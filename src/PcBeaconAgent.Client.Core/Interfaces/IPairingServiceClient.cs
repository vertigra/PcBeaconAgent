using PcBeaconAgent.Contracts.Models;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Core.Interfaces
{
    /// <summary>
    /// HTTP client for the server's pairing endpoint. Unlike
    /// <see cref="IAudioServiceClient"/> and <see cref="IDisplayServiceClient"/>,
    /// this client is not bound to a single device — pairing happens before
    /// the device is added to <c>ManagedDevices</c>, so the IP and port are
    /// passed per call.
    /// </summary>
    public interface IPairingServiceClient
    {
        /// <summary>
        /// Submits the PIN to the server and, on success, returns the API key.
        /// </summary>
        /// <param name="ip">Server IP address.</param>
        /// <param name="port">Server API port.</param>
        /// <param name="pin">The 6-digit PIN shown in the server log / tray.</param>
        /// <returns>The API key on success, or <c>null</c> if the PIN was wrong or pairing is inactive.</returns>
        Task<PairResponseDto?> PairAsync(string ip, int port, string pin);

        /// <summary>
        /// Requests a fresh PIN from the server. The new PIN appears in the
        /// server log / tray window. Returns <c>true</c> if the server
        /// accepted the request.
        /// </summary>
        Task<bool> RegeneratePinAsync(string ip, int port);
    }
}
