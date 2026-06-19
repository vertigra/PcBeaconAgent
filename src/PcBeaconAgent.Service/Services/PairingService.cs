using Microsoft.Extensions.Logging;
using PcBeaconAgent.Service.Interfaces;
using System;

namespace PcBeaconAgent.Service.Services
{
    public class PairingService : IPairingService
    {
        private readonly IBeaconAnnouncementService mBeacon;
        private readonly ILogger<PairingService> mLogger;

        private string mPin = string.Empty;
        private DateTime mPinExpiry;
        private bool mPinUsed;
        private int mFailedAttempts;

        // Maximum failed PIN attempts before pairing locks out.
        // Prevents brute-forcing 10^6 combinations over the LAN.
        private const int MaxFailedAttempts = 5;

        // PIN validity window. Short enough to limit exposure,
        // long enough for the user to read it from the console.
        private static readonly TimeSpan PinLifetime = TimeSpan.FromMinutes(5);

        public bool IsPairingActive =>
            !mPinUsed &&
            mFailedAttempts < MaxFailedAttempts &&
            DateTime.UtcNow < mPinExpiry;

        public PairingService(IBeaconAnnouncementService beacon, ILogger<PairingService> logger)
        {
            mBeacon = beacon;
            mLogger = logger;
            GeneratePin();
        }

        /// <inheritdoc />
        public string? ValidateAndExchangePin(string pin)
        {
            if (!IsPairingActive)
            {
                LogPairingInactive();
                return null;
            }

            if (!string.Equals(pin.Trim(), mPin, StringComparison.Ordinal))
            {
                mFailedAttempts++;
                int remaining = MaxFailedAttempts - mFailedAttempts;

                if (remaining > 0)
                    LogInvalidPin(remaining);
                else
                    LogPairingLocked();

                return null;
            }

            // PIN is correct — single-use: invalidate immediately.
            mPinUsed = true;
            LogPairingSuccess();
            return mBeacon.ApiKey;
        }

        /// <inheritdoc />
        public void RegeneratePin()
        {
            GeneratePin();
        }

        private void GeneratePin()
        {
            // 6 digits: 100 000–999 999, easy to type on a phone keyboard.
            mPin = Random.Shared.Next(100_000, 1_000_000).ToString();
            mPinExpiry = DateTime.UtcNow.Add(PinLifetime);
            mPinUsed = false;
            mFailedAttempts = 0;

            // Prominent separator makes the PIN easy to spot in a scrolling log.
            LogNewPin(mPin, (int)PinLifetime.TotalMinutes);
        }

        // --- Structured logging definitions (allocation-free) ---

        private static readonly Action<ILogger, string, int, Exception?> LogNewPinAction =
            LoggerMessage.Define<string, int>(
                LogLevel.Warning,
                new EventId(10, "PairingPinGenerated"),
                "══════════════════════════════════════\n" +
                "  PAIRING PIN : {Pin}\n" +
                "  Valid for   : {Minutes} minutes\n" +
                "══════════════════════════════════════");

        private static readonly Action<ILogger, int, Exception?> LogInvalidPinAction =
            LoggerMessage.Define<int>(
                LogLevel.Warning,
                new EventId(11, "PairingPinInvalid"),
                "Pairing attempt failed — invalid PIN. Attempts remaining: {Remaining}");

        private static readonly Action<ILogger, Exception?> LogPairingLockedAction =
            LoggerMessage.Define(
                LogLevel.Error,
                new EventId(12, "PairingLocked"),
                "Pairing locked after too many failed attempts. Restart the service to reset.");

        private static readonly Action<ILogger, Exception?> LogPairingInactiveAction =
            LoggerMessage.Define(
                LogLevel.Warning,
                new EventId(13, "PairingInactive"),
                "Pairing attempt rejected — pairing mode is not active (PIN expired or already used).");

        private static readonly Action<ILogger, Exception?> LogPairingSuccessAction =
            LoggerMessage.Define(
                LogLevel.Information,
                new EventId(14, "PairingSuccess"),
                "Pairing successful. ApiKey exchanged and PIN invalidated.");

        private void LogNewPin(string pin, int minutes) => LogNewPinAction(mLogger, pin, minutes, null);
        private void LogInvalidPin(int remaining) => LogInvalidPinAction(mLogger, remaining, null);
        private void LogPairingLocked() => LogPairingLockedAction(mLogger, null);
        private void LogPairingInactive() => LogPairingInactiveAction(mLogger, null);
        private void LogPairingSuccess() => LogPairingSuccessAction(mLogger, null);
    }
}