using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace PcBeaconAgent.Client.Core.Models;

/// <summary>
/// Represents a discovered PC agent device within the local network.
/// </summary>
public partial class BeaconDevice : ObservableObject
{
    /// <summary>
    /// Gets or sets the network host name of the computer (e.g., DESKTOP-MAIN).
    /// </summary>
    [ObservableProperty]
    public partial string MachineName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the local IP address of the agent discovered via UDP.
    /// </summary>
    [ObservableProperty]
    public partial string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the port number where the Web API is listening (default is 5000).
    /// </summary>
    [ObservableProperty]
    public partial int ApiPort { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the current availability status of the device (e.g., "Online", "Offline").
    /// </summary>
    [ObservableProperty]
    public partial string Status { get; set; } = "Online";

    /// <summary>
    /// Gets or sets the timestamp of the last received beacon response from this device.
    /// </summary>
    [ObservableProperty]
    public partial DateTime LastSeen { get; set; } = DateTime.UtcNow;
}