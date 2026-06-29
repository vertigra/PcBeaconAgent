namespace PcBeaconAgent.Client.Core.Interfaces
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
    }
}