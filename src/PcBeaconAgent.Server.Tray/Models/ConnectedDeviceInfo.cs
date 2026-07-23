using System;

namespace PcBeaconAgent.Server.Tray.Models
{
    /// <summary>
    /// Lightweight view-model-friendly wrapper around a connected
    /// SignalR client. Used by <see cref="ViewModels.FilesViewModel"/>
    /// to populate the "Send to phone" device picker. One instance per
    /// connected Android device.
    /// </summary>
    public sealed class ConnectedDeviceInfo
    {
        /// <summary>
        /// SignalR connection ID — passed to
        /// <c>TransferController.SendTextToClientAsync</c> /
        /// <c>SendFileToClientAsync</c> as the target.
        /// </summary>
        public string ConnectionId { get; init; }

        /// <summary>
        /// Display name — the client's machine name (e.g. "Pixel 7")
        /// if provided, otherwise the client's IP address.
        /// </summary>
        public string DisplayName { get; init; }

        /// <summary>
        /// Client's IP address (for display in the picker tooltip).
        /// </summary>
        public string? RemoteIp { get; init; }

        public ConnectedDeviceInfo(string connectionId, string? machineName, string? remoteIp)
        {
            ConnectionId = connectionId;
            DisplayName = string.IsNullOrEmpty(machineName) ? (remoteIp ?? "Unknown") : machineName;
            RemoteIp = remoteIp;
        }
    }
}
