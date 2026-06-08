using System;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models;

namespace PcBeaconAgent.Client.Core.Services;

/// <summary>
/// Implements UDP broadcast discovery to find active PC beacon agents in the local network.
/// </summary>
public partial class UdpDiscoveryService : IUdpDiscoveryService, IDisposable
{
    private readonly int mPort;
    private readonly string mDiscoveryRequestPayload = "PING_PC_BEACON_AGENT";
    private readonly ObservableCollection<BeaconDevice> mInternalDevices;
    private readonly Lock mLock = new();
    private readonly ILogger<UdpDiscoveryService> mLogger;
    private readonly JsonSerializerOptions mJsonOptions;
    private CancellationTokenSource? mTokenSource;
    private UdpClient? mListenerClient; // Dedicated client ONLY for listening to the port
    private bool mIsScanning;

    /// <inheritdoc />
    public ReadOnlyObservableCollection<BeaconDevice> DiscoveredDevices { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UdpDiscoveryService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for tracking network activity.</param>
    /// <param name="port">The UDP port to use for broadcast and discovery. Defaults to 53535.</param>
    public UdpDiscoveryService(ILogger<UdpDiscoveryService> logger, int port = 53535)
    {
        mLogger = logger;
        mPort = port;
        mInternalDevices = [];
        DiscoveredDevices = new ReadOnlyObservableCollection<BeaconDevice>(mInternalDevices);

        mJsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <inheritdoc />
    public Task StartScanningAsync(CancellationToken cancellationToken)
    {
        lock (mLock)
        {
            if (mIsScanning) return Task.CompletedTask;
            mIsScanning = true;
        }

        LogStartingService(mLogger, mPort);
        mTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            // Initialize a dedicated socket for RECEIVING responses bound firmly to the port.
            mListenerClient = new UdpClient();
            mListenerClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            mListenerClient.Client.Bind(new IPEndPoint(IPAddress.Any, mPort));

            // Start background tasks
            Task.Run(() => ListenForBeaconsAsync(mTokenSource.Token), mTokenSource.Token);
            Task.Run(() => BroadcastDiscoveryRequestAsync(mTokenSource.Token), mTokenSource.Token);

            LogServiceInitialized(mLogger);
        }
        catch (Exception ex)
        {
            LogInitializationFailed(mLogger, mPort, ex);
            StopScanning();
            throw;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void StopScanning()
    {
        lock (mLock)
        {
            if (!mIsScanning) return;
            mIsScanning = false;
        }

        LogStoppingService(mLogger);

        mTokenSource?.Cancel();
        mTokenSource?.Dispose();
        mTokenSource = null;

        try
        {
            mListenerClient?.Close();
            mListenerClient?.Dispose();
        }
        catch (Exception ex)
        {
            LogCloseListenerException(mLogger, ex);
        }
        finally
        {
            mListenerClient = null;
        }

        LogServiceStopped(mLogger);
    }

    /// <inheritdoc />
    public void CheckTimeouts()
    {
        lock (mLock)
        {
            var now = DateTime.UtcNow;
            foreach (var device in mInternalDevices)
            {
                if (now - device.LastSeen > TimeSpan.FromSeconds(15))
                {
                    if (device.Status != "Offline")
                    {
                        LogDeviceTimeout(mLogger, device.MachineName, device.IpAddress);
                        device.Status = "Offline";
                    }
                }
            }
        }
    }

    /// <summary>
    /// Background loop for sending pings. Uses an ephemeral socket for reliable operation on Android/Linux.
    /// </summary>
    private async Task BroadcastDiscoveryRequestAsync(CancellationToken token)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(mDiscoveryRequestPayload);
        LogSenderLoopStarted(mLogger);

        while (!token.IsCancellationRequested)
        {
            // For SENDING, we use a separate ephemeral socket without a Bind call, allowing the OS
            // to manage routing tables and active interfaces automatically.
            using (var senderClient = new UdpClient())
            {
                try
                {
                    senderClient.EnableBroadcast = true;

                    // Universal broadcast endpoint. Mobile OS will map this to the active Wi-Fi network.
                    var endpoint = new IPEndPoint(IPAddress.Broadcast, mPort);

                    LogSendingBroadcast(mLogger, endpoint);
                    await senderClient.SendAsync(buffer, endpoint, token);
                }
                catch (Exception) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogBroadcastSendFailed(mLogger, ex);
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Background loop for listening to incoming responses from agents.
    /// </summary>
    private async Task ListenForBeaconsAsync(CancellationToken token)
    {
        LogListenerLoopStarted(mLogger);

        while (!token.IsCancellationRequested)
        {
            try
            {
                // Local copy of the reference to prevent race conditions when Stop is invoked.
                var client = mListenerClient;
                if (client == null) break;

                var result = await client.ReceiveAsync(token);
                string message = Encoding.UTF8.GetString(result.Buffer);

                // Ignore echo packets from our own broadcasts
                if (message == mDiscoveryRequestPayload) continue;

                LogPacketReceived(mLogger, result.RemoteEndPoint, result.Buffer.Length);

                var incomingDevice = JsonSerializer.Deserialize<BeaconDevice>(message, mJsonOptions);
                if (incomingDevice != null)
                {
                    incomingDevice.IpAddress = result.RemoteEndPoint.Address.ToString();
                    ProcessDiscoveredDevice(incomingDevice);
                }
            }
            catch (Exception) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogIncomingPayloadParseFailed(mLogger, ex);
            }
        }
    }

    private void ProcessDiscoveredDevice(BeaconDevice incoming)
    {
        lock (mLock)
        {
            var existing = FindDeviceByMachineName(incoming.MachineName);

            if (existing != null)
            {
                existing.IpAddress = incoming.IpAddress;
                existing.ApiPort = incoming.ApiPort;
                existing.LastSeen = DateTime.UtcNow;

                if (existing.Status != "Online")
                {
                    LogDeviceCameBackOnline(mLogger, existing.MachineName);
                    existing.Status = "Online";
                }
            }
            else
            {
                LogDiscoveredNewDevice(mLogger, incoming.MachineName, incoming.IpAddress, incoming.ApiPort);
                incoming.LastSeen = DateTime.UtcNow;
                incoming.Status = "Online";
                mInternalDevices.Add(incoming);
            }
        }
    }

    private BeaconDevice? FindDeviceByMachineName(string machineName)
    {
        foreach (var device in mInternalDevices)
        {
            if (device.MachineName.Equals(machineName, StringComparison.OrdinalIgnoreCase))
            {
                return device;
            }
        }
        return null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        StopScanning();
        GC.SuppressFinalize(this);
    }

    // --- High-performance Logger Messages Generation (Fixes CS8795, CA1873 allocations) ---

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Starting UDP discovery service on port {Port}...")]
    private static partial void LogStartingService(ILogger logger, int port);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "UDP discovery background tasks initialized successfully.")]
    private static partial void LogServiceInitialized(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Stopping UDP discovery service...")]
    private static partial void LogStoppingService(ILogger logger);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "UDP discovery service stopped.")]
    private static partial void LogServiceStopped(ILogger logger);

    [LoggerMessage(EventId = 5, Level = LogLevel.Debug, Message = "Discovery broadcast sender loop started.")]
    private static partial void LogSenderLoopStarted(ILogger logger);

    [LoggerMessage(EventId = 6, Level = LogLevel.Trace, Message = "Sending global UDP broadcast to: {Endpoint}")]
    private static partial void LogSendingBroadcast(ILogger logger, IPEndPoint endpoint);

    [LoggerMessage(EventId = 7, Level = LogLevel.Debug, Message = "Discovery incoming payload listener loop started.")]
    private static partial void LogListenerLoopStarted(ILogger logger);

    [LoggerMessage(EventId = 8, Level = LogLevel.Trace, Message = "Received UDP packet from {Endpoint}. Length: {Length} bytes.")]
    private static partial void LogPacketReceived(ILogger logger, IPEndPoint endpoint, int length);

    [LoggerMessage(EventId = 9, Level = LogLevel.Debug, Message = "Failed to send UDP broadcast ping via sender socket.")]
    private static partial void LogBroadcastSendFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 10, Level = LogLevel.Warning, Message = "Failed to parse or process incoming UDP discovery response payload.")]
    private static partial void LogIncomingPayloadParseFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 11, Level = LogLevel.Error, Message = "Failed to initialize UDP client or bind to port {Port}.")]
    private static partial void LogInitializationFailed(ILogger logger, int port, Exception ex);

    [LoggerMessage(EventId = 12, Level = LogLevel.Debug, Message = "Exception while closing listener UDP client.")]
    private static partial void LogCloseListenerException(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 13, Level = LogLevel.Warning, Message = "Device {MachineName} ({IpAddress}) missing for more than 15s. Marking as Offline.")]
    private static partial void LogDeviceTimeout(ILogger logger, string machineName, string ipAddress);

    [LoggerMessage(EventId = 14, Level = LogLevel.Information, Message = "Device {MachineName} came back Online.")]
    private static partial void LogDeviceCameBackOnline(ILogger logger, string machineName);

    [LoggerMessage(EventId = 15, Level = LogLevel.Information, Message = "Discovered new agent device: {MachineName} at {IpAddress}:{ApiPort}")]
    private static partial void LogDiscoveredNewDevice(ILogger logger, string machineName, string ipAddress, int apiPort);
}