using PcBeaconAgent.Contracts.Models;
using System;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Core.Interfaces
{
    /// <summary>
    /// Defines a contract for discovering network devices via UDP broadcast.
    /// </summary>
    public interface IBeaconClient
    {
        /// <summary>
        /// Event triggered when a beacon device is successfully discovered.
        /// </summary>
        event Action<DiscoveredBeacon>? OnBeaconFound;

        /// <summary>
        /// Performs a network-wide broadcast scan to discover active beacons.
        /// </summary>
        /// <param name="timeoutMs">Timeout in milliseconds for the response wait.</param>
        /// <returns>The discovered device or null if no response was received.</returns>
        Task ScanAsync(int timeoutMs = 2000);
    }
}