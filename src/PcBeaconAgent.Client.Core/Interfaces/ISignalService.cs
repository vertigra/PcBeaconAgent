using PcBeaconAgent.Client.Core.Models;
using System;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Core.Interfaces;

/// <summary>
/// Provides a centralized service for managing multiple SignalR hub connections.
/// Handles connection lifecycles, event routing, and data transmission.
/// </summary>
public interface ISignalService
{
    /// <summary>
    /// Triggered when detailed information about a device is received from the hub.
    /// </summary>
    event Action<string, BeaconDevice>? DeviceDetailsReceived;

    /// <summary>
    /// Triggered when the online/offline status of a specific device changes.
    /// </summary>
    event Action<string, bool>? DeviceStatusChanged;


    /// <summary>
    /// Establishes a connection to the beacon hub using the provided <see cref="BeaconDevice"/> model.
    /// </summary>
    /// <param name="beaconDevice">The device containing network configuration details.</param>
    Task ConnectToBeaconHubAsync(BeaconDevice beaconDevice);

    /// <summary>
    /// Gracefully closes and disposes of the connection associated with the given IP address.
    /// </summary>
    /// <param name="ipAddress">The IP address of the device to disconnect.</param>
    Task DisconnectBeaconHubAsync(string ipAddress);

    /// <summary>
    /// Gracefully closes and disposes of the connection for the specified device.
    /// </summary>
    /// <param name="beaconDevice">The device to disconnect.</param>
    Task DisconnectBeaconHubAsync(BeaconDevice beaconDevice);

    /// <summary>
    /// Sends a command to the specified hub along with accompanying data.
    /// </summary>
    /// <param name="ipAddress">The IP address of the target device.</param>
    /// <param name="command">The name of the hub method to invoke.</param>
    /// <param name="data">The object payload to send.</param>
    Task SendCommandAsync(string ipAddress, string command, object data);

    /// <summary>
    /// Sends a command to the specified hub without additional data.
    /// </summary>
    /// <param name="ipAddress">The IP address of the target device.</param>
    /// <param name="command">The name of the hub method to invoke.</param>
    Task SendCommandAsync(string ipAddress, string command);

    /// <summary>
    /// Establishes a connection, requests device details, and immediately closes the connection.
    /// Useful for single-shot state snapshots.
    /// </summary>
    /// <param name="beaconDevice">The device to query.</param>
    Task ReceiveDeviceDetailsAndCloseAsync(BeaconDevice beaconDevice);

    /// <summary>
    /// Disconnects (if currently connected) and permanently removes the stored
    /// pairing key for the given device. Use this specifically when the user
    /// explicitly forgets a device — a plain Disconnect (e.g. triggered by app
    /// sleep) must NOT remove the key, otherwise the device would require
    /// re-pairing every time the app goes to background.
    /// </summary>
    Task ForgetAsync(string ipAddress);
}