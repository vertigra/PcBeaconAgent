using System;

namespace PcBeaconAgent.Client.Core.Models
{
    /// <summary>
    /// Kind of a received transfer — mirrors the server's
    /// TransferKind but lives in Client.Core (no server dependency).
    /// </summary>
    public enum ReceivedTransferKind
    {
        Text,
        File
    }

    /// <summary>
    /// In-memory record of a transfer received from the PC via
    /// SignalR push. Stored in <see cref="Stores.ReceivedTransferStore"/>
    /// and displayed on the Android Received page.
    /// </summary>
    public sealed class ReceivedTransfer
    {
        public string Id { get; init; } = string.Empty;
        public ReceivedTransferKind Kind { get; init; }
        public string Text { get; init; } = string.Empty;
        public string FileName { get; init; } = string.Empty;
        public string FilePath { get; init; } = string.Empty;
        public long SizeBytes { get; init; }
        public DateTime ReceivedAtUtc { get; init; }
        public string SourceMachine { get; init; } = string.Empty;

        /// <summary>
        /// Human-readable size string (e.g. "1.2 KB"). Computed once
        /// at construction for UI display.
        /// </summary>
        public string DisplaySize => FormatFileSize(SizeBytes);

        /// <summary>
        /// Short time string for the UI list (local time, HH:mm).
        /// </summary>
        public string DisplayTime => ReceivedAtUtc.ToLocalTime().ToString("HH:mm");

        private static string FormatFileSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int unitIndex = 0;
            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }
            return unitIndex == 0 ? $"{(int)size} {units[unitIndex]}" : $"{size:F1} {units[unitIndex]}";
        }
    }
}
