using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace PcBeaconAgent.Client.Core.Models
{

    /// <summary>
    /// Represents a discovered PC agent device within the local network.
    /// Holds network configuration, hardware identification, and UI status metrics.
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
        /// This is extracted from the remote endpoint of the incoming network packet.
        /// </summary>
        [ObservableProperty]
        public partial string IpAddress { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the port number where the Web API is listening (default is 5000).
        /// Used by the client to establish direct HTTP REST connection with the agent service.
        /// </summary>
        [ObservableProperty]
        public partial int ApiPort { get; set; } = 5000;

        /// <summary>
        /// Gets or sets the physical MAC address of the active network adapter on the remote PC.
        /// Used as a hardware-level identifier to uniquely track machines even if their IP changes.
        /// Format example: 00:1A:2B:3C:4D:5E.
        /// </summary>
        [ObservableProperty]
        public partial string MacAddress { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the system display name of the remote network interface card (NIC).
        /// Represents the OS-assigned adapter name in Windows (e.g., "Ethernet 2", "Wi-Fi").
        /// </summary>
        [ObservableProperty]
        public partial string InterfaceName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the connection medium type of the active interface (e.g., "Wi-Fi", "Ethernet").
        /// Used by the UI layout to visually distinguish wireless clients from wired ones.
        /// </summary>
        [ObservableProperty]
        public partial string InterfaceType { get; set; } = string.Empty;
    }
}