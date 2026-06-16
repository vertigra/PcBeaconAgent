using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models;

namespace PcBeaconAgent.Client.Core.Services;

/// <inheritdoc />
public class SignalService : ISignalService
{
    private readonly Dictionary<string, HubConnection> mConnections = new();
    private readonly ILogger<SignalService> mLogger;

    public event Action<string, BeaconDevice>? DeviceDetailsReceived;
    public event Action<string>? ServerRequestedDisconnect;

    public SignalService(ILogger<SignalService> logger)
    {
        mLogger = logger;
    }

    /// <inheritdoc />
    public async Task ConnectAsync(string ipAddress, string hubUrl, CancellationToken ct = default)
    {
        if (mConnections.ContainsKey(ipAddress))
        {
            mLogger.LogWarning("Connection for {Ip} already exists, skipping.", ipAddress);
            return;
        }

        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        connection.On<BeaconDevice>("ReceiveDeviceDetails", (json) =>
        {
            try
            {
                if (json != null) 
                    DeviceDetailsReceived?.Invoke(ipAddress, json);
            }
            catch (Exception ex)
            {
                mLogger.LogError(ex, "Failed to deserialize device details for {Id}", ipAddress);
            }
        });

        connection.On("CloseConnection", () =>
        {
            ServerRequestedDisconnect?.Invoke(ipAddress);
        });

        mConnections[ipAddress] = connection;

        try
        {
            await connection.StartAsync(ct);
            mLogger.LogInformation("Connected to hub: {Id}", ipAddress);
        }
        catch (Exception ex)
        {
            mConnections.Remove(ipAddress);
            mLogger.LogError(ex, "Could not connect to {Id}", ipAddress);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(string ipAddress)
    {
        if (mConnections.TryGetValue(ipAddress, out var connection))
        {
            await connection.StopAsync();
            await connection.DisposeAsync();
            mConnections.Remove(ipAddress);
            mLogger.LogInformation("Disconnected from {Id}", ipAddress);
        }
    }

    /// <inheritdoc />
    public async Task SendCommandAsync(string ipAddress, string command, object data)
    {
        if (mConnections.TryGetValue(ipAddress, out var connection) &&
            connection.State == HubConnectionState.Connected)
        {
            await connection.SendAsync(command, data);
        }
    }
}