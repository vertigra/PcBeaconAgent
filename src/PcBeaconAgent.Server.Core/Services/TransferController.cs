using Microsoft.Extensions.Logging;
using PcBeaconAgent.Server.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace PcBeaconAgent.Server.Core.Services
{
    /// <summary>
    /// Receives text transfers from the Android client, stores them in
    /// an in-memory ring buffer (capped at <see cref="MaxHistoryItems"/>),
    /// and raises <see cref="TransferReceived"/> so the tray host can
    /// show a toast and auto-copy short payloads to the clipboard.
    /// </summary>
    /// <remarks>
    /// <b>Thread safety.</b> All mutable state is guarded by
    /// <see cref="mStateLock"/>. Events are raised OUTSIDE the lock to
    /// avoid deadlock — the same pattern as <see cref="PairingService"/>.
    /// <para>
    /// <b>Lifetime.</b> Registered as a singleton so the history
    /// survives across HTTP requests and so the tray host can
    /// subscribe once at startup.
    /// </para>
    /// </remarks>
    public sealed class TransferController
    {
        private readonly ILogger<TransferController> mLogger;

        // LinkedList (not Queue) because we need to snapshot the history
        // for GetHistory() without materialising a new collection on
        // every call — LinkedList allows O(1) add-to-tail + remove-from-
        // head while still being enumerable directly.
        private readonly LinkedList<TransferRecord> mHistory = new();

        // Guards mHistory. HTTP request threads can call ReceiveText
        // concurrently — without this lock, two concurrent receives
        // could interleave AddLast and produce a corrupted list.
        private readonly Lock mStateLock = new();

        /// <summary>
        /// Maximum accepted text payload size, measured in UTF-8 bytes.
        /// 100 KB is well above any reasonable clipboard snippet while
        /// keeping a single request from consuming significant memory.
        /// Files (Phase 2) will have a separate, larger cap.
        /// </summary>
        public const int MaxTextSizeBytes = 100 * 1024;

        /// <summary>
        /// Maximum number of history items retained. When a new item
        /// would exceed this count, the oldest item is evicted. 100 is
        /// enough to scroll back through a session's worth of transfers
        /// without unbounded memory growth.
        /// </summary>
        public const int MaxHistoryItems = 100;

        /// <summary>
        /// Raised when a text transfer is accepted and stored. Raised
        /// OUTSIDE <see cref="mStateLock"/> so subscribers can safely
        /// acquire other locks (including re-entering this controller
        /// via <see cref="GetHistory"/>) without deadlock.
        /// </summary>
        public event Action<TransferRecord>? TransferReceived;

        public TransferController(ILogger<TransferController> logger)
        {
            mLogger = logger;
        }

        /// <summary>
        /// Accepts a text payload, validates it, stores it in history,
        /// and raises <see cref="TransferReceived"/>.
        /// </summary>
        /// <param name="text">The text payload. Must not be null, empty,
        /// or whitespace-only, and must not exceed
        /// <see cref="MaxTextSizeBytes"/> when UTF-8 encoded.</param>
        /// <param name="sourceIp">Client IP from the HTTP layer.</param>
        /// <returns>A tuple of (accepted, message). <c>accepted</c> is
        /// <c>true</c> when the transfer was stored; <c>message</c> is
        /// a short human-readable status suitable for the client
        /// UI.</returns>
        public (bool Accepted, string Message) ReceiveText(string text, string sourceIp)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                LogEmptyPayload();
                return (false, "Text payload is empty.");
            }

            int byteCount = Encoding.UTF8.GetByteCount(text);
            if (byteCount > MaxTextSizeBytes)
            {
                LogPayloadTooLarge(byteCount);
                return (false, $"Payload too large ({byteCount} bytes; limit {MaxTextSizeBytes}).");
            }

            TransferRecord record = new(
                id: Guid.NewGuid().ToString("N"),
                text: text,
                receivedAtUtc: DateTime.UtcNow,
                sourceIp: string.IsNullOrEmpty(sourceIp) ? "unknown" : sourceIp);

            // Capture the event delegate reference outside the lock so
            // we raise it after releasing the lock — same pattern as
            // PairingService.ValidateAndExchangePin. Raising inside the
            // lock risks deadlock if a subscriber tries to re-enter the
            // controller (e.g. via GetHistory) or acquires another lock
            // that the HTTP thread holds.
            var handler = TransferReceived;

            lock (mStateLock)
            {
                mHistory.AddLast(record);

                // Evict oldest if over cap. LinkedList.RemoveFirst is
                // O(1). We evict one at a time because ReceiveText is
                // called once per request — bulk eviction is not needed.
                if (mHistory.Count > MaxHistoryItems)
                {
                    mHistory.RemoveFirst();
                }
            }

            LogTransferReceived(record.Id, byteCount, record.SourceIp);
            handler?.Invoke(record);
            return (true, "Transfer received.");
        }

        /// <summary>
        /// Returns a snapshot of the current history, newest first.
        /// The snapshot is a copy — the caller can iterate without
        /// holding the lock and without seeing concurrent mutations.
        /// </summary>
        public IReadOnlyList<TransferRecord> GetHistory()
        {
            lock (mStateLock)
            {
                // Materialise a List<TransferRecord> with newest-first
                // ordering. The UI shows the most recent transfer at
                // the top, so Reverse() once here is cheaper than
                // reversing on every bind.
                return mHistory.Reverse().ToList();
            }
        }

        #region Structured logging definitions (allocation-free)

        private static readonly Action<ILogger, string, int, string, Exception?> LogTransferReceivedAction =
            LoggerMessage.Define<string, int, string>(
                LogLevel.Information,
                new EventId(20, "TransferReceived"),
                "Text transfer {TransferId} received ({ByteCount} bytes from {SourceIp}).");

        private static readonly Action<ILogger, int, Exception?> LogPayloadTooLargeAction =
            LoggerMessage.Define<int>(
                LogLevel.Warning,
                new EventId(21, "TransferTooLarge"),
                "Text transfer rejected — payload {ByteCount} bytes exceeds limit.");

        private static readonly Action<ILogger, Exception?> LogEmptyPayloadAction =
            LoggerMessage.Define(
                LogLevel.Warning,
                new EventId(22, "TransferEmpty"),
                "Text transfer rejected — payload is empty or whitespace-only.");

        private void LogTransferReceived(string id, int byteCount, string sourceIp) =>
            LogTransferReceivedAction(mLogger, id, byteCount, sourceIp, null);

        private void LogPayloadTooLarge(int byteCount) =>
            LogPayloadTooLargeAction(mLogger, byteCount, null);

        private void LogEmptyPayload() =>
            LogEmptyPayloadAction(mLogger, null);

        #endregion
    }
}
