using System;

namespace PcBeaconAgent.Server.Core.Models
{
    /// <summary>
    /// In-memory record of a received text transfer. Stored in the
    /// <see cref="Services.TransferController"/> history ring and
    /// passed to subscribers of
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
        /// Full text payload. Kept verbatim so the tray UI can offer
        /// "copy again" on history items. The size is bounded by
        /// <see cref="Services.TransferController.MaxTextSizeBytes"/>.
        /// </summary>
        public string Text { get; init; }

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

        public TransferRecord(string id, string text, DateTime receivedAtUtc, string sourceIp)
        {
            Id = id;
            Text = text;
            ReceivedAtUtc = receivedAtUtc;
            SourceIp = sourceIp;
        }
    }
}
