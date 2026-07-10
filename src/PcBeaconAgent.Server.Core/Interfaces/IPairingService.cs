using System;
using PcBeaconAgent.Server.Core.Events;

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
        /// Returns the UTC instant at which the current PIN expires, or
        /// <c>null</c> if no PIN is active. Used by the tray popup to render
        /// an accurate countdown when the user reopens it manually (the
        /// Generated event carries the original expiry, but a click on the
        /// tray icon happens later and needs the current remaining time).
        /// </summary>
        DateTime? GetCurrentPinExpiryUtc();

        /// <summary>
        /// Raised on every PIN lifecycle transition: Generated (new PIN),
        /// Used (successful exchange), Expired (lifetime elapsed), Locked
        /// (too many failed attempts). Subscribers must marshal to the UI
        /// thread themselves — the event is raised on a thread-pool thread
        /// for Expired, and on the caller's thread for the other states.
        /// </summary>
        event Action<PairingStateEventArgs>? PairingStateChanged;
    }
}
