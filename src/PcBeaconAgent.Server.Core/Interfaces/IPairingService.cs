using System;

namespace PcBeaconAgent.Server.Core.Interfaces
{
    /// <summary>
    /// Manages a single-use, time-limited PIN for initial client pairing.
    /// The PIN is generated automatically on service start and displayed in the log.
    /// A successful exchange returns the ApiKey and immediately invalidates the PIN.
    /// </summary>
    public interface IPairingService
    {
        /// <summary>True while the PIN exists, has not been used, and has not expired.</summary>
        bool IsPairingActive { get; }

        /// <summary>
        /// Validates <paramref name="pin"/> and, if correct, returns the ApiKey
        /// and invalidates the PIN. Returns null on failure.
        /// </summary>
        string? ValidateAndExchangePin(string pin);

        /// <summary>
        /// Generates a new PIN, resetting the expiry window and the failed-attempt counter.
        /// Call this when the user requests re-pairing.
        /// </summary>
        void RegeneratePin();

        /// <summary>
        /// Returns the current PIN string, or an empty string if no PIN is
        /// active (expired, used, or not yet generated). Used by the tray
        /// host to display the PIN in the UI.
        /// </summary>
        string GetCurrentPin();

        /// <summary>
        /// Raised when the PIN state changes: a new PIN is generated,
        /// the PIN is used (successful pairing), or the PIN expires.
        /// The tray host subscribes to show/hide balloon notifications.
        /// </summary>
        event Action<PairingStateEventArgs>? PairingStateChanged;
    }

    public class PairingStateEventArgs : EventArgs
    {
        public PairingState State { get; init; }
        public string Pin { get; init; } = string.Empty;
        public DateTime ExpiryUtc { get; init; }
    }

    public enum PairingState
    {
        Generated,
        Used,
        Expired,
        Locked
    }
}