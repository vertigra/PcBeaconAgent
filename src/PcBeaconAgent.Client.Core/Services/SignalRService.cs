using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using PcBeaconAgent.Client.Core.Interfaces;

namespace PcBeaconAgent.Client.Core.Services;

/// <inheritdoc />
public class SignalRService : ISignalRService
{
    private HubConnection? mHubConnection;
    private string? mServerUrl;

    /// <inheritdoc />
    public event EventHandler<bool>? ConnectionChanged;

    /// <inheritdoc />
    public bool IsConnected => mHubConnection?.State == HubConnectionState.Connected;

    /// <inheritdoc />
    public void Configure(string serverUrl)
    {
        if (string.IsNullOrWhiteSpace(serverUrl))
            throw new ArgumentException("Server URL cannot be empty.", nameof(serverUrl));

        mServerUrl = serverUrl;

        // Initialize the member connection with automatic reconnect
        mHubConnection = new HubConnectionBuilder()
            .WithUrl(mServerUrl)
            .WithAutomaticReconnect()
            .Build();

        mHubConnection.Closed += OnConnectionClosed;
        mHubConnection.Reconnected += OnReconnected;
    }

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (mHubConnection == null)
            throw new InvalidOperationException("Service must be configured with a URL before connecting.");

        await mHubConnection.StartAsync(cancellationToken);
        ConnectionChanged?.Invoke(this, true);
    }

    /// <inheritdoc />
    public async Task DisconnectAsync()
    {
        if (mHubConnection != null)
        {
            await mHubConnection.StopAsync();
            ConnectionChanged?.Invoke(this, false);
        }
    }

    /// <inheritdoc />
    public async Task SendCommandAsync(string command, object data)
    {
        if (IsConnected && mHubConnection != null)
        {
            await mHubConnection.SendAsync(command, data);
        }
    }

    private Task OnConnectionClosed(Exception? exception)
    {
        ConnectionChanged?.Invoke(this, false);
        return Task.CompletedTask;
    }

    private Task OnReconnected(string? connectionId)
    {
        ConnectionChanged?.Invoke(this, true);
        return Task.CompletedTask;
    }
}