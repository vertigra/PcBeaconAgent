using System;
using System.Threading;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Core.Interfaces;

/// <summary>
/// A robust SignalR client implementation that provides real-time bi-directional communication.
/// Manages connection lifecycle, automatic reconnection, and command dispatching
/// between the agent and the management server.
/// </summary>
public interface ISignalRService
{
    /// <summary>
    /// Gets a value indicating whether the client is currently connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Configures the service with the target server URL found via discovery.
    /// </summary>
    void Configure(string serverUrl);

    /// <summary>
    /// Establishes a connection to the server.
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gracefully closes the connection.
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// Sends a command to the server hub.
    /// </summary>
    Task SendCommandAsync(string command, object data);

    /// <summary>
    /// Event triggered when the connection state changes.
    /// </summary>
    event EventHandler<bool>? ConnectionChanged;
}