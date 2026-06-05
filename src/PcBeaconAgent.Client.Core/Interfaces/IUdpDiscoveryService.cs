using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using PcBeaconAgent.Client.Core.Models;

namespace PcBeaconAgent.Client.Core.Interfaces;

/// <summary>
/// Defines a service capable of discovering PC beacon agents within the local network using UDP broadcast.
/// </summary>
public interface IUdpDiscoveryService
{
    /// <summary>
    /// Gets the collection of currently discovered and active devices.
    /// </summary>
    ReadOnlyObservableCollection<BeaconDevice> DiscoveredDevices { get; }

    /// <summary>
    /// Starts the background scanning process for discovering local agents.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task StartScanningAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops the scanning process and clears resources.
    /// </summary>
    void StopScanning();

    /// <summary>
    /// Forces a check on all discovered devices and marks them "Offline" if they haven't responded within 15 seconds.
    /// Call this from the UI tier only when the view is active.
    /// </summary>
    void CheckTimeouts();
}