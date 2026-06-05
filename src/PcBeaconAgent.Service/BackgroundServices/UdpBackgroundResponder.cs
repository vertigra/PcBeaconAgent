using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PcBeaconAgent.Service.Configuration; // Подтягиваем твои AppSettings
using PcBeaconAgent.Service.Models;

namespace PcBeaconAgent.Service.BackgroundServices;

/// <summary>
/// Background service that listens for UDP discovery pings and responds with the agent's identity and API port.
/// </summary>
public partial class UdpBackgroundResponder : BackgroundService
{
    private readonly AppSettings mSettings;
    private readonly string mDiscoveryRequestPayload = "PING_PC_BEACON_AGENT";
    private readonly ILogger<UdpBackgroundResponder> mLogger;
    private readonly JsonSerializerOptions mJsonOptions;
    private readonly string mMachineName;

    /// <summary>
    /// Initializes a new instance of the <see cref="UdpBackgroundResponder"/> class.
    /// </summary>
    public UdpBackgroundResponder(AppSettings settings, ILogger<UdpBackgroundResponder> logger)
    {
        mSettings = settings;
        mLogger = logger;
        mMachineName = Environment.MachineName;

        mJsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int udpPort = mSettings.Server.DiscoveryPort;

        if (mLogger.IsEnabled(LogLevel.Information))
            mLogger.LogInformation("Starting UDP Responder on port {Port}...", udpPort);

        using var udpServer = new UdpClient();

        try
        {
            udpServer.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpServer.Client.Bind(new IPEndPoint(IPAddress.Any, udpPort));

            if (OperatingSystem.IsWindows())
            {
                const int SIO_UDP_CONNRESET = -1744830452;
                udpServer.Client.IOControl(SIO_UDP_CONNRESET, [0], null);
            }

            if (mLogger.IsEnabled(LogLevel.Information))
                mLogger.LogInformation("UDP Responder successfully bound to port {Port}.", udpPort);
        }
        catch (Exception ex)
        {
            LogResponderLoopError(mLogger, ex);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await udpServer.ReceiveAsync(stoppingToken);
                string message = Encoding.UTF8.GetString(result.Buffer);

                if (message == mDiscoveryRequestPayload)
                {
                    if (mLogger.IsEnabled(LogLevel.Information))
                        mLogger.LogInformation("Discovery request received from {ClientEndpoint}", result.RemoteEndPoint);

                    var responseData = new UdpBeaconResponse
                    {
                        MachineName = mMachineName,
                        ApiPort = mSettings.Server.Port
                    };

                    string jsonResponse = JsonSerializer.Serialize(responseData, mJsonOptions);
                    byte[] responseBuffer = Encoding.UTF8.GetBytes(jsonResponse);

                    await udpServer.SendAsync(responseBuffer, responseBuffer.Length, result.RemoteEndPoint);

                    if (mLogger.IsEnabled(LogLevel.Trace))
                        mLogger.LogTrace("Sent JSON discovery response to {ClientEndpoint}.", result.RemoteEndPoint);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
            {
                if (mLogger.IsEnabled(LogLevel.Debug))
                    mLogger.LogDebug("Ignored Windows Winsock 10054 Connection Reset error.");
            }
            catch (Exception ex)
            {
                mLogger.LogError(ex, "Error occurred in UDP Responder loop.");
                await Task.Delay(2000, stoppingToken);
            }
        }

        if (mLogger.IsEnabled(LogLevel.Information))
            mLogger.LogInformation("UDP Responder on port {Port} stopped.", udpPort);
    }

    [LoggerMessage(
        EventId = 101,
        Level = LogLevel.Error,
        Message = "Error occurred in UDP background responder loop.")]
    private static partial void LogResponderLoopError(ILogger logger, Exception ex);
}