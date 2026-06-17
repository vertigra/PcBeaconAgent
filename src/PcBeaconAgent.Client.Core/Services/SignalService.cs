using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models;

namespace PcBeaconAgent.Client.Core.Services;

public class SignalService(ILogger<SignalService> mLogger) : ISignalService
{
    public event Action<string, BeaconDevice>? DeviceDetailsReceived;
    public event Action<string, bool>? DeviceStatusChanged;

    private readonly Dictionary<string, HubConnection> mConnections = [];

    /// <inheritdoc />
    public async Task ConnectToBeaconHubAsync(BeaconDevice beaconDevice)
    {
        string hubUrl = $"http://{beaconDevice.IpAddress}:{beaconDevice.ApiPort}/hubs/beacon";
        await ConnectAsync(beaconDevice.IpAddress, hubUrl);
    }

    /// <inheritdoc />
    public async Task DisconnectBeaconHubAsync(string ipAddress)
    {
        if (mConnections.Remove(ipAddress, out var connection))
        {
            await connection.StopAsync();
            await connection.DisposeAsync();
            LogConnectionStopped(ipAddress);
        }
    }

    /// <inheritdoc />
    public async Task DisconnectBeaconHubAsync(BeaconDevice beaconDevice)
    {
        await DisconnectBeaconHubAsync(beaconDevice.IpAddress);
    }

    /// <inheritdoc />
    public async Task SendCommandAsync(string ipAddress, string command, object data)
    {
        if (mConnections.TryGetValue(ipAddress, out var connection) && connection.State == HubConnectionState.Connected)
            await connection.SendAsync(command, data);
    }

    /// <inheritdoc />
    public async Task SendCommandAsync(string ipAddress, string command)
    {
        if (mConnections.TryGetValue(ipAddress, out var connection) && connection.State == HubConnectionState.Connected)
            await connection.SendAsync(command);
    }

    /// <inheritdoc />
    public async Task ReceiveDeviceDetailsAndCloseAsync(BeaconDevice beaconDevice)
    {
        string hubUrl = $"http://{beaconDevice.IpAddress}:{beaconDevice.ApiPort}/hubs/beacon";
        await ConnectAsync(beaconDevice.IpAddress, hubUrl);
        await SendCommandAsync(beaconDevice.IpAddress, "ReceiveDeviceDetailsAndClose");
    }

    /// <summary>
    /// Establishes a new connection to a hub at the specified address.
    /// Supports automatic reconnection.
    /// </summary>
    /// <param name="ipAddress">The IP address of the target device (used as a key).</param>
    /// <param name="hubUrl">The full URL of the SignalR hub.</param>
    /// <param name="ct">Cancellation token for the connection process.</param>

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
            LogConnectionError(ipAddress, ex);
            DeviceStatusChanged?.Invoke(ipAddress, false);
        }
    }

    private HubConnection CreateConnection(string ipAddress, string hubUrl)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        connection.Closed += (_) =>
        {
            DeviceStatusChanged?.Invoke(ipAddress, false);
            return Task.CompletedTask;
        };

        connection.Reconnecting += (_) =>
        {
            DeviceStatusChanged?.Invoke(ipAddress, false);
            return Task.CompletedTask;
        };

        connection.Reconnected += (_) =>
        {
            DeviceStatusChanged?.Invoke(ipAddress, true);
            return Task.CompletedTask;
        };

        connection.On<BeaconDevice>("ReceiveDeviceDetails", data => DeviceDetailsReceived?.Invoke(ipAddress, data));
        connection.On("CloseConnection", async () => await DisconnectBeaconHubAsync(ipAddress));

        return connection;
    }


    private static readonly Action<ILogger, string, string, Exception?> LogErrorAction =
            LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(1, "ConnectionError"), "Connection error for {IpAddress}: {Message}");

    private static readonly Action<ILogger, string, Exception?> LogStartedAction =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2, "ConnectionStarted"), "Connection started for {IpAddress}");

    private static readonly Action<ILogger, string, Exception?> LogStoppedAction =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(3, "ConnectionStopped"), "Connection stopped for {IpAddress}");

    private void LogConnectionError(string ip, Exception ex) => LogErrorAction(mLogger, ip, ex.Message, ex);

    private void LogConnectionStarted(string ip) => LogStartedAction(mLogger, ip, null);

    private void LogConnectionStopped(string ip) => LogStoppedAction(mLogger, ip, null);
}