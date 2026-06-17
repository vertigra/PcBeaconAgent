using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models;

namespace PcBeaconAgent.Client.Core.Services;

public class SignalService : ISignalService
{
    private readonly Dictionary<string, HubConnection> mConnections = [];
    private readonly ILogger<SignalService> mLogger;

    public event Action<string, BeaconDevice>? DeviceDetailsReceived;
    public event Action<string, bool>? DeviceStatusChanged;

    public SignalService(ILogger<SignalService> logger)
    {
        mLogger = logger;
    }

    public async Task ConnectAsync(string ipAddress, string hubUrl, CancellationToken ct = default)
    {
        if (mConnections.TryGetValue(ipAddress, out var connection))
        {
            if (connection.State == HubConnectionState.Connected || connection.State == HubConnectionState.Connecting)
                return;

            await DisconnectBeaconHubAsync(ipAddress);
        }

        var newConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        newConnection.Closed += (ex) => {
            DeviceStatusChanged?.Invoke(ipAddress, false);
            return Task.CompletedTask;
        };

        newConnection.Reconnecting += (ex) => {
            DeviceStatusChanged?.Invoke(ipAddress, false);
            return Task.CompletedTask;
        };

        newConnection.Reconnected += (id) => {
            DeviceStatusChanged?.Invoke(ipAddress, true);
            return Task.CompletedTask;
        };

        newConnection.On<BeaconDevice>("ReceiveDeviceDetails", async (data) => {
            DeviceDetailsReceived?.Invoke(ipAddress, data);
        });

        newConnection.On("CloseConnection", async () => {
            await DisconnectBeaconHubAsync(ipAddress);
        });

        mConnections[ipAddress] = newConnection;

        try
        {
            await newConnection.StartAsync(ct);
            LogConnectionStarted(ipAddress);
            DeviceStatusChanged?.Invoke(ipAddress, true);
        }
        catch (Exception ex)
        {
            LogConnectionError(ipAddress, ex);
            DeviceStatusChanged?.Invoke(ipAddress, false);
        }
    }

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


    private static readonly Action<ILogger, string, string, Exception?> LogErrorAction =
            LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(1, "ConnectionError"), "Connection error for {IpAddress}: {Message}");

    private static readonly Action<ILogger, string, Exception?> LogStartedAction =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2, "ConnectionStarted"), "Connection started for {IpAddress}");

    private static readonly Action<ILogger, string, Exception?> LogStoppedAction =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(3, "ConnectionStopped"), "Connection stopped for {IpAddress}");

    private void LogConnectionError(string ip, Exception ex)
        => LogErrorAction(mLogger, ip, ex.Message, ex);

    private void LogConnectionStarted(string ip)
        => LogStartedAction(mLogger, ip, null);

    private void LogConnectionStopped(string ip)
        => LogStoppedAction(mLogger, ip, null);
}