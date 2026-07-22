using System;

namespace PcBeaconAgent.Server.Core.Models
{
    /// <summary>
    /// Discriminates text and file transfers in the history ring.
    /// The tray UI uses this to decide which row template to render
    /// (text preview + Copy button vs file name + Open folder button).
    /// </summary>
    public enum TransferKind
    {
        /// <summary>
        /// Text payload — <see cref="TransferRecord.Text"/> is populated,
        /// <see cref="TransferRecord.FileName"/> is empty.
        /// </summary>
        Text,

        /// <summary>
        /// File payload — <see cref="TransferRecord.FileName"/> and
        /// <see cref="TransferRecord.SavedFilePath"/> are populated,
        /// <see cref="TransferRecord.Text"/> is empty.
        /// </summary>
        File
    }

    /// <summary>
    /// In-memory record of a received transfer (text or file). Stored
    /// in the <see cref="Services.TransferController"/> history ring
    /// and passed to subscribers of
    /// <see cref="Services.TransferController.TransferReceived"/>.
    /// </summary>
    public sealed class TransferRecord
    {
        /// <summary>
        /// Unique identifier (GUID) for this transfer. Used by the tray
        /// UI as a key when listing history items.
        /// </summary>
        public string Id { get; init; }

        /// <summary>
        /// Transfer kind — text or file. Drives which fields are
        /// populated and which tray UI row template is used.
        /// </summary>
        public TransferKind Kind { get; init; }

        /// <summary>
        /// Full text payload for <see cref="TransferKind.Text"/> records.
        /// Empty for <see cref="TransferKind.File"/> records. Kept
        /// verbatim so the tray UI can offer "copy again" on history
        /// items. The size is bounded by
        /// <see cref="Services.TransferController.MaxTextSizeBytes"/>.
        /// </summary>
        public string Text { get; init; }

        /// <summary>
        /// Original file name (sanitised — basename only, no path) for
        /// <see cref="TransferKind.File"/> records. Empty for
        /// <see cref="TransferKind.Text"/> records.
        /// </summary>
        public string FileName { get; init; }

        /// <summary>
        /// Full path on disk where the file was saved, for
        /// <see cref="TransferKind.File"/> records. Empty for
        /// <see cref="TransferKind.Text"/> records. The tray UI uses
        /// this to offer "Open folder".
        /// </summary>
        public string SavedFilePath { get; init; }

        /// <summary>
        /// Size of the payload in bytes. For text, this is the UTF-8
        /// byte count. For files, the file size on disk. Used by the
        /// tray UI for display and by logging.
        /// </summary>
        public long SizeBytes { get; init; }

        /// <summary>
        /// UTC timestamp at which the server received the transfer.
        /// Used for history display and ordering.
        /// </summary>
        public DateTime ReceivedAtUtc { get; init; }

        /// <summary>
        /// Source IP of the client that sent the transfer. Extracted
        /// from <c>HttpContext.Connection.RemoteIpAddress</c> at the
        /// endpoint layer. <c>"unknown"</c> if the IP cannot be
        /// determined (unusual — should not happen on a normal LAN
        /// connection).
        /// </summary>
        public string SourceIp { get; init; }

        public TransferRecord(
            string id,
            TransferKind kind,
            string text,
            string fileName,
            string savedFilePath,
            long sizeBytes,
            DateTime receivedAtUtc,
            string sourceIp)
        {
            Id = id;
            Kind = kind;
            Text = text;
            FileName = fileName;
            SavedFilePath = savedFilePath;
            SizeBytes = sizeBytes;
            ReceivedAtUtc = receivedAtUtc;
            SourceIp = string.IsNullOrEmpty(sourceIp) ? "unknown" : sourceIp;
        }
    }
}
