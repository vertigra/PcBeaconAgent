using PcBeaconAgent.Contracts.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Core.Interfaces;

public interface ISignalService
{

    event Action<string, bool>? DeviceStatusChanged;

    /// <summary>
    /// Raised when the PC pushes a text transfer to this client via
    /// SignalR. Parameters: (sourceIp, text, sourceMachineName).
    /// The sourceIp is the PC's IP — used to resolve the API key
    /// for any follow-up HTTP calls.
    /// </summary>
    event Action<string, string, string>? TextTransferReceived;

    /// <summary>
    /// Raised when the PC pushes a file transfer notification via
    /// SignalR. Parameters: (sourceIp, fileName, sizeBytes,
    /// downloadUrl, sourceMachineName). The client must HTTP GET
    /// the downloadUrl (with X-Api-Key header) to fetch the file
    /// content.
    /// </summary>
    event Action<string, string, long, string, string>? FileTransferReceived;

    /// <summary>
    /// Machine name of this client, sent to the server via the
    /// <c>machine</c> query-string parameter on the SignalR
    /// handshake. The server uses it to label connected devices
    /// in the tray UI. Set this before calling
    /// <see cref="ConnectToBeaconHubAsync"/>.
    /// </summary>
    string ClientMachineName { get; set; }

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