using Microsoft.Extensions.Logging;
using PcBeaconAgent.Server.Core.Configuration;
using PcBeaconAgent.Server.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace PcBeaconAgent.Server.Core.Services
{
    /// <summary>
    /// Receives text and file transfers from the Android client, stores
    /// them in an in-memory ring buffer (capped at
    /// <see cref="MaxHistoryItems"/>), and raises
    /// <see cref="TransferReceived"/> so the tray host can show a toast
    /// and auto-copy short text payloads to the clipboard. File
    /// payloads are streamed to <see cref="TransferSettings.SaveFolder"/>
    /// — only metadata stays in memory.
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
        private readonly TransferSettings mSettings;

        // LinkedList (not Queue) because we need to snapshot the history
        // for GetHistory() without materialising a new collection on
        // every call — LinkedList allows O(1) add-to-tail + remove-from-
        // head while still being enumerable directly.
        private readonly LinkedList<TransferRecord> mHistory = new();

        // Guards mHistory. HTTP request threads can call ReceiveText /
        // ReceiveFile concurrently — without this lock, two concurrent
        // receives could interleave AddLast and produce a corrupted list.
        private readonly Lock mStateLock = new();

        /// <summary>
        /// Maximum accepted text payload size, measured in UTF-8 bytes.
        /// 100 KB is well above any reasonable clipboard snippet while
        /// keeping a single request from consuming significant memory.
        /// Files have no size cap (per user decision — LAN, user
        /// trusts the source) but are streamed to disk so memory usage
        /// stays bounded regardless of file size.
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
        /// Raised when a transfer (text or file) is accepted and stored.
        /// Raised OUTSIDE <see cref="mStateLock"/> so subscribers can
        /// safely acquire other locks (including re-entering this
        /// controller via <see cref="GetHistory"/>) without deadlock.
        /// </summary>
        public event Action<TransferRecord>? TransferReceived;

        public TransferController(ILogger<TransferController> logger, TransferSettings settings)
        {
            mLogger = logger;
            mSettings = settings;
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
                kind: TransferKind.Text,
                text: text,
                fileName: string.Empty,
                savedFilePath: string.Empty,
                sizeBytes: byteCount,
                receivedAtUtc: DateTime.UtcNow,
                sourceIp: sourceIp);

            StoreAndRaise(record);
            LogTextTransferReceived(record.Id, byteCount, record.SourceIp);
            return (true, "Transfer received.");
        }

        /// <summary>
        /// Accepts a file payload (as a stream), saves it to the
        /// configured <see cref="TransferSettings.SaveFolder"/>, stores
        /// a metadata record in history, and raises
        /// <see cref="TransferReceived"/>.
        /// </summary>
        /// <param name="stream">The file content stream. Will be
        /// read to completion and disposed by the caller (the HTTP
        /// layer owns the stream lifetime).</param>
        /// <param name="fileName">Original file name from the client.
        /// Sanitised to basename only (path components stripped) to
        /// prevent path traversal outside the save folder.</param>
        /// <param name="sourceIp">Client IP from the HTTP layer.</param>
        /// <returns>A tuple of (accepted, message, savedFileName).
        /// <c>savedFileName</c> is the actual name on disk (may differ
        /// from the input if a collision occurred — numeric suffix
        /// appended). Empty when <c>accepted</c> is <c>false</c>.</returns>
        public (bool Accepted, string Message, string SavedFileName) ReceiveFile(Stream stream, string fileName, string sourceIp)
        {
            if (stream == null)
            {
                LogEmptyPayload();
                return (false, "File stream is null.", string.Empty);
            }

            // Sanitise the file name — strip any directory components
            // the client may have sent. Path.GetFileName handles both
            // forward and back slashes on all platforms. If the result
            // is empty (e.g. the client sent a pure path), fall back to
            // a generic name so the file is still saved.
            string safeName = SanitiseFileName(fileName);
            if (string.IsNullOrEmpty(safeName))
            {
                safeName = $"transfer-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
            }

            string saveFolder = ResolveSaveFolder();
            if (string.IsNullOrEmpty(saveFolder))
            {
                LogSaveFolderError();
                return (false, "Save folder is not configured.", string.Empty);
            }

            try
            {
                Directory.CreateDirectory(saveFolder);
            }
            catch (Exception ex)
            {
                LogSaveFolderCreateError(ex);
                return (false, "Could not create save folder.", string.Empty);
            }

            // If a file with the same name already exists, append a
            // numeric suffix to avoid overwriting. The suffix loop is
            // bounded — in practice collisions are rare.
            string finalPath = Path.Combine(saveFolder, safeName);
            if (File.Exists(finalPath))
            {
                string baseName = Path.GetFileNameWithoutExtension(safeName);
                string ext = Path.GetExtension(safeName);
                int suffix = 1;
                while (File.Exists(finalPath))
                {
                    finalPath = Path.Combine(saveFolder, $"{baseName} ({suffix}){ext}");
                    suffix++;
                }
            }

            long bytesWritten;
            try
            {
                using var fs = new FileStream(finalPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: false);
                stream.CopyTo(fs, 81920);
                bytesWritten = fs.Length;
            }
            catch (Exception ex)
            {
                LogFileSaveError(ex);
                return (false, "Could not save the file.", string.Empty);
            }

            string savedName = Path.GetFileName(finalPath);

            TransferRecord record = new(
                id: Guid.NewGuid().ToString("N"),
                kind: TransferKind.File,
                text: string.Empty,
                fileName: savedName,
                savedFilePath: finalPath,
                sizeBytes: bytesWritten,
                receivedAtUtc: DateTime.UtcNow,
                sourceIp: sourceIp);

            StoreAndRaise(record);
            LogFileTransferReceived(record.Id, savedName, bytesWritten, record.SourceIp);
            return (true, $"File received: {savedName}", savedName);
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
                return [.. mHistory.Reverse()];
            }
        }

        // ── Helpers ──────────────────────────────────────────────

        private void StoreAndRaise(TransferRecord record)
        {
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
                // O(1). We evict one at a time because ReceiveText /
                // ReceiveFile is called once per request.
                if (mHistory.Count > MaxHistoryItems)
                {
                    mHistory.RemoveFirst();
                }
            }

            handler?.Invoke(record);
        }

        /// <summary>
        /// Strips any directory components from the file name so the
        /// client cannot write outside the save folder via a crafted
        /// path like <c>..\..\Windows\System32\evil.dll</c> or an
        /// absolute path like <c>C:\Windows\evil.dll</c>.
        /// <see cref="Path.GetFileName"/> handles both separators on
        /// all platforms. Returns an empty string if the input was
        /// pure path with no file name.
        /// </summary>
        private static string SanitiseFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return string.Empty;

            // Path.GetFileName strips directory components. Additional
            // trimming removes any residual whitespace that could
            // cause issues on some file systems.
            string baseName = Path.GetFileName(fileName.Trim());

            // Reject reserved Windows file names (CON, PRN, AUX, NUL,
            // COM1-9, LPT1-9) — they would fail to save on Windows.
            // Path.GetFileName already handles them on Windows, but we
            // double-check for cross-platform safety.
            if (IsReservedWindowsName(baseName))
                return $"transfer-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

            return baseName;
        }

        private static bool IsReservedWindowsName(string name)
        {
            // Strip extension for the reserved-name check.
            string stem = Path.GetFileNameWithoutExtension(name);
            return stem.ToUpperInvariant() is "CON" or "PRN" or "AUX" or "NUL"
                or "COM1" or "COM2" or "COM3" or "COM4" or "COM5"
                or "COM6" or "COM7" or "COM8" or "COM9"
                or "LPT1" or "LPT2" or "LPT3" or "LPT4" or "LPT5"
                or "LPT6" or "LPT7" or "LPT8" or "LPT9";
        }

        /// <summary>
        /// Resolves the save folder by expanding the
        /// <c>%USERPROFILE%</c> placeholder to the actual user profile
        /// path. Returns an empty string if the placeholder cannot be
        /// expanded (e.g. running under an account without a profile).
        /// </summary>
        private string ResolveSaveFolder()
        {
            string raw = mSettings.SaveFolder;
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(userProfile)) return raw;

            return raw.Replace("%USERPROFILE%", userProfile, StringComparison.OrdinalIgnoreCase);
        }

        #region Structured logging definitions (allocation-free)

        private static readonly Action<ILogger, string, int, string, Exception?> LogTextTransferReceivedAction =
            LoggerMessage.Define<string, int, string>(
                LogLevel.Information,
                new EventId(20, "TextTransferReceived"),
                "Text transfer {TransferId} received ({ByteCount} bytes from {SourceIp}).");

        private static readonly Action<ILogger, string, string, long, string, Exception?> LogFileTransferReceivedAction =
            LoggerMessage.Define<string, string, long, string>(
                LogLevel.Information,
                new EventId(23, "FileTransferReceived"),
                "File transfer {TransferId} received: {FileName} ({ByteCount} bytes from {SourceIp}).");

        private static readonly Action<ILogger, int, Exception?> LogPayloadTooLargeAction =
            LoggerMessage.Define<int>(
                LogLevel.Warning,
                new EventId(21, "TransferTooLarge"),
                "Text transfer rejected — payload {ByteCount} bytes exceeds limit.");

        private static readonly Action<ILogger, Exception?> LogEmptyPayloadAction =
            LoggerMessage.Define(
                LogLevel.Warning,
                new EventId(22, "TransferEmpty"),
                "Transfer rejected — payload is empty or stream is null.");

        private static readonly Action<ILogger, Exception?> LogSaveFolderErrorAction =
            LoggerMessage.Define(
                LogLevel.Error,
                new EventId(24, "SaveFolderNotConfigured"),
                "File transfer rejected — save folder is not configured.");

        private static readonly Action<ILogger, Exception?> LogSaveFolderCreateErrorAction =
            LoggerMessage.Define(
                LogLevel.Error,
                new EventId(25, "SaveFolderCreateError"),
                "Could not create the configured save folder.");

        private static readonly Action<ILogger, Exception?> LogFileSaveErrorAction =
            LoggerMessage.Define(
                LogLevel.Error,
                new EventId(26, "FileSaveError"),
                "Could not save the received file to disk.");

        private void LogTextTransferReceived(string id, int byteCount, string sourceIp) =>
            LogTextTransferReceivedAction(mLogger, id, byteCount, sourceIp, null);

        private void LogFileTransferReceived(string id, string fileName, long byteCount, string sourceIp) =>
            LogFileTransferReceivedAction(mLogger, id, fileName, byteCount, sourceIp, null);

        private void LogPayloadTooLarge(int byteCount) =>
            LogPayloadTooLargeAction(mLogger, byteCount, null);

        private void LogEmptyPayload() =>
            LogEmptyPayloadAction(mLogger, null);

        private void LogSaveFolderError() =>
            LogSaveFolderErrorAction(mLogger, null);

        private void LogSaveFolderCreateError(Exception ex) =>
            LogSaveFolderCreateErrorAction(mLogger, ex);

        private void LogFileSaveError(Exception ex) =>
            LogFileSaveErrorAction(mLogger, ex);

        #endregion
    }
}
