using System;
using System.Threading;
using System.Threading.Tasks;
using PcBeaconAgent.Client.Core.Models;

namespace PcBeaconAgent.Client.Core.Interfaces;

/// <summary>
/// Provides a centralized service for managing multiple SignalR hub connections.
/// Handles connection lifecycle, event routing, and data deserialization internally.
/// </summary>
public interface ISignalService
{
    /// <summary>
    /// Triggered when valid device details are received from a specific hub.
    /// </summary>
    event Action<string, BeaconDevice>? DeviceDetailsReceived;

    /// <summary>
    /// Triggered when the server requests a graceful disconnection.
    /// </summary>
    event Action<string>? ServerRequestedDisconnect;

    /// <summary>
    /// Establishes a new connection to a hub and starts listening for commands.
    /// </summary>
    Task ConnectAsync(string ipAddress, string hubUrl, CancellationToken ct = default);

    /// <summary>
    /// Closes and disposes of a specific hub connection.
    /// </summary>
    Task DisconnectAsync(string ipAddress);

    /// <summary>
    /// Sends a generic command to the specified hub.
    /// </summary>
    Task SendCommandAsync(string ipAddress, string command, object data);
}