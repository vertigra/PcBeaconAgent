using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PcBeaconAgent.Client.Core.Constants;
using PcBeaconAgent.Client.Core.Exceptions;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Contracts;
using PcBeaconAgent.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Core.Services;

public class SignalService(ILogger<SignalService> mLogger, IPreferencesService mPrefs) : ISignalService
{
    public event Action<string, bool>? DeviceStatusChanged;
    public event Action<string, string, string>? TextTransferReceived;
    public event Action<string, string, long, string, string>? FileTransferReceived;

    public string ClientMachineName { get; set; } = string.Empty;

    private readonly Dictionary<string, HubConnection> mConnections = [];

    private static readonly TimeSpan DeviceDetailsTimeout = TimeSpan.FromSeconds(5);

    public async Task ConnectToBeaconHubAsync(BeaconDevice beaconDevice)
    {
        string hubUrl = $"http://{beaconDevice.IpAddress}:{beaconDevice.ApiPort}/hubs/beacon";

        // Append the machine name as a query-string parameter so the
        // server can label this connection in the tray UI. URL-encode
        // to handle spaces / special chars in device model names.
        if (!string.IsNullOrEmpty(ClientMachineName))
        {
            hubUrl += $"?machine={Uri.EscapeDataString(ClientMachineName)}";
        }

        await ConnectAsync(beaconDevice.IpAddress, hubUrl);
    }

    public async Task DisconnectBeaconHubAsync(string ipAddress)
    {
        if (mConnections.Remove(ipAddress, out var connection))
        {
            await connection.StopAsync();
            await connection.DisposeAsync();
            LogConnectionStopped(ipAddress);
        }
    }

    public async Task DisconnectBeaconHubAsync(BeaconDevice beaconDevice)
        => await DisconnectBeaconHubAsync(beaconDevice.IpAddress);

    public async Task ForgetAsync(string ipAddress)
    {
        await DisconnectBeaconHubAsync(ipAddress);
        mPrefs.Remove(StorageKeys.ApiKeyFor(ipAddress));
        LogKeyForgotten(ipAddress);
    }

    public async Task SendCommandAsync(string ipAddress, string command, object data)
    {
        if (mConnections.TryGetValue(ipAddress, out var connection) &&
            connection.State == HubConnectionState.Connected)
            await connection.SendAsync(command, data);
    }

    public async Task SendCommandAsync(string ipAddress, string command)
    {
        if (mConnections.TryGetValue(ipAddress, out var connection) &&
            connection.State == HubConnectionState.Connected)
            await connection.SendAsync(command);
    }

    /// <inheritdoc />
    public async Task<BeaconDevice> ConnectAndFetchDetailsAsync(BeaconDevice beaconDevice, CancellationToken ct = default)
    {
        await ConnectToBeaconHubAsync(beaconDevice);
        return await RefreshDeviceDetailsAsync(beaconDevice.IpAddress, ct);
    }

    /// <inheritdoc />
    public async Task<BeaconDevice> RefreshDeviceDetailsAsync(string ipAddress, CancellationToken ct = default)
    {
        if (!mConnections.TryGetValue(ipAddress, out var connection) ||
            connection.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException($"No active connection for {ipAddress}.");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(DeviceDetailsTimeout);

        try
        {
            return await connection.InvokeAsync<BeaconDevice>("GetDeviceDetails", timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"Timed out waiting for device details from {ipAddress}.");
        }
    }

    private async Task ConnectAsync(string ipAddress, string hubUrl, CancellationToken ct = default)
    {
        if (mConnections.TryGetValue(ipAddress, out var conn))
        {
            if (conn.State is HubConnectionState.Connected or HubConnectionState.Connecting)
                return;
            await DisconnectBeaconHubAsync(ipAddress);
        }

        var connection = CreateConnection(ipAddress, hubUrl);
        mConnections[ipAddress] = connection;

        try
        {
            await connection.StartAsync(ct);
            LogConnectionStarted(ipAddress);
            DeviceStatusChanged?.Invoke(ipAddress, true);
        }
        catch (Exception ex)
        {
            mConnections.Remove(ipAddress);
            LogConnectionError(ipAddress, ex);
            DeviceStatusChanged?.Invoke(ipAddress, false);
            throw;
        }
    }

    private HubConnection CreateConnection(string ipAddress, string hubUrl)
    {
        var apiKey = ResolveApiKey(ipAddress);

        if (string.IsNullOrEmpty(apiKey))
            throw new NotPairedException();

        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.Headers["X-Api-Key"] = apiKey;
            })
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.TypeInfoResolverChain.Clear();
                options.PayloadSerializerOptions.TypeInfoResolverChain.Add(ProjectJsonContext.Default);
            })
            .WithAutomaticReconnect()
            .Build();

        connection.Closed += _ =>
        {
            DeviceStatusChanged?.Invoke(ipAddress, false);
            return Task.CompletedTask;
        };

        connection.Reconnecting += _ =>
        {
            DeviceStatusChanged?.Invoke(ipAddress, false);
            return Task.CompletedTask;
        };

        connection.Reconnected += _ =>
        {
            DeviceStatusChanged?.Invoke(ipAddress, true);
            return Task.CompletedTask;
        };

        // ── Push event handlers (PC → Android) ──────────────────────
        // The server pushes these events via SignalR when the tray
        // host sends a text or file transfer to this client. The
        // ipAddress (the PC's IP) is captured in the closure so the
        // subscriber can resolve the API key for file downloads.

        connection.On<string, string>("ReceiveTextTransfer", (text, sourceMachine) =>
        {
            TextTransferReceived?.Invoke(ipAddress, text, sourceMachine);
        });

        connection.On<string, int, string, string>("ReceiveFileTransfer", (fileName, sizeBytes, downloadUrl, sourceMachine) =>
        {
            FileTransferReceived?.Invoke(ipAddress, fileName, sizeBytes, downloadUrl, sourceMachine);
        });

        return connection;
    }

    private string? ResolveApiKey(string ipAddress)
    {
        var scoped = mPrefs.Get(StorageKeys.ApiKeyFor(ipAddress), string.Empty);
        if (!string.IsNullOrEmpty(scoped))
            return scoped;

        var global = mPrefs.Get(StorageKeys.ApiKey, string.Empty);
        return string.IsNullOrEmpty(global) ? null : global;
    }

    #region Structured logging definitions (allocation-free)

    private static readonly Action<ILogger, string, string, Exception?> LogErrorAction =
        LoggerMessage.Define<string, string>(LogLevel.Error,
            new EventId(1, "ConnectionError"), "Connection error for {IpAddress}: {Message}");

    private static readonly Action<ILogger, string, Exception?> LogStartedAction =
        LoggerMessage.Define<string>(LogLevel.Debug,
            new EventId(2, "ConnectionStarted"), "Connection started for {IpAddress}");

    private static readonly Action<ILogger, string, Exception?> LogStoppedAction =
        LoggerMessage.Define<string>(LogLevel.Debug,
            new EventId(3, "ConnectionStopped"), "Connection stopped for {IpAddress}");

    private static readonly Action<ILogger, string, Exception?> LogKeyForgottenAction =
        LoggerMessage.Define<string>(LogLevel.Information,
            new EventId(5, "KeyForgotten"), "Removed stored pairing key for {IpAddress}");

    private void LogConnectionError(string ip, Exception ex) => LogErrorAction(mLogger, ip, ex.Message, ex);
    private void LogConnectionStarted(string ip) => LogStartedAction(mLogger, ip, null);
    private void LogConnectionStopped(string ip) => LogStoppedAction(mLogger, ip, null);
    private void LogKeyForgotten(string ip) => LogKeyForgottenAction(mLogger, ip, null);

    #endregion
}