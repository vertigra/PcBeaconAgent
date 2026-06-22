using System;
using System.Threading;
using System.Threading.Tasks;
using PcBeaconAgent.Client.Core.Models;

namespace PcBeaconAgent.Client.Core.Interfaces;

public interface ISignalService
{

    event Action<string, bool>? DeviceStatusChanged;

    Task ConnectToBeaconHubAsync(BeaconDevice beaconDevice);
    Task DisconnectBeaconHubAsync(string ipAddress);
    Task DisconnectBeaconHubAsync(BeaconDevice beaconDevice);
    Task ForgetAsync(string ipAddress);
    Task SendCommandAsync(string ipAddress, string command, object data);
    Task SendCommandAsync(string ipAddress, string command);

    /// <summary>
    /// Establishes a persistent connection and requests device details via a
    /// direct RPC call (Hub method with a return value) — not a push-and-wait
    /// pattern. The connection stays open afterward.
    /// </summary>
    /// <exception cref="Exceptions.NotPairedException">No API key stored for this device.</exception>
    /// <exception cref="TimeoutException">No reply within the timeout window.</exception>
    Task<BeaconDevice> ConnectAndFetchDetailsAsync(BeaconDevice beaconDevice, CancellationToken ct = default);

    /// <summary>
    /// Requests fresh device details from an already-connected device via the
    /// same RPC call. Throws if there is no active connection for this IP.
    /// </summary>
    Task<BeaconDevice> RefreshDeviceDetailsAsync(string ipAddress, CancellationToken ct = default);
}