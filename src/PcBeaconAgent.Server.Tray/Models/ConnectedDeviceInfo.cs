using System;

namespace PcBeaconAgent.Server.Tray.Models
{
    /// <summary>
    /// Lightweight view-model-friendly wrapper around a known client
    /// (may be online or offline). Used by
    /// <see cref="ViewModels.FilesViewModel"/> to populate the device
    /// picker in the "Send to phone" section.
    /// </summary>
    public sealed class ConnectedDeviceInfo
    {
        /// <summary>
        /// Client IP address — used as the stable identifier for
        /// TransferController.SendTextToClientAsync /
        /// SendFileToClientAsync (not the SignalR connection ID, which
        /// changes on every reconnect).
        /// </summary>
        public string ClientIp { get; init; }

        /// <summary>
        /// Display name — the client's machine name (e.g. "Pixel 7")
        /// if provided, otherwise the client's IP address.
        /// </summary>
        public string DisplayName { get; init; }

        /// <summary>
        /// True if the client is currently connected (SignalR
        /// connection active). False if the client was previously
        /// seen but is now offline. Offline clients can still receive
        /// queued transfers (pending replay on reconnect).
        /// </summary>
        public bool IsOnline { get; set; }

        public ConnectedDeviceInfo(string clientIp, string? machineName, bool isOnline)
        {
            ClientIp = clientIp;
            DisplayName = string.IsNullOrEmpty(machineName) ? clientIp : machineName;
            IsOnline = isOnline;
        }
    }
}
