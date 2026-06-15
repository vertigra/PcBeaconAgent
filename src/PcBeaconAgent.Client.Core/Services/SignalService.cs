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
    public async Task ConnectAsync(string connectionId, string hubUrl, CancellationToken ct = default)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        connection.On<BeaconDevice>("ReceiveDeviceDetails", (json) =>
        {
            try
            {
                if (json != null) 
                    DeviceDetailsReceived?.Invoke(connectionId, json);
            }
            catch (Exception ex)
            {
                mLogger.LogError(ex, "Failed to deserialize device details for {Id}", connectionId);
            }
        });

        connection.On("CloseConnection", () =>
        {
            ServerRequestedDisconnect?.Invoke(connectionId);
        });

        mConnections[connectionId] = connection;

        try
        {
            await connection.StartAsync(ct);
            mLogger.LogInformation("Connected to hub: {Id}", connectionId);
        }
        catch (Exception ex)
        {
            mConnections.Remove(connectionId);
            mLogger.LogError(ex, "Could not connect to {Id}", connectionId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(string connectionId)
    {
        if (mConnections.TryGetValue(connectionId, out var connection))
        {
            await connection.StopAsync();
            await connection.DisposeAsync();
            mConnections.Remove(connectionId);
            mLogger.LogInformation("Disconnected from {Id}", connectionId);
        }
    }

    /// <inheritdoc />
    public async Task SendCommandAsync(string connectionId, string command, object data)
    {
        if (mConnections.TryGetValue(connectionId, out var connection) &&
            connection.State == HubConnectionState.Connected)
        {
            await connection.SendAsync(command, data);
        }
    }
}