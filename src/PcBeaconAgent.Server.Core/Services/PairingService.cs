using Microsoft.Extensions.Logging;
using PcBeaconAgent.Server.Core.Interfaces;
using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace PcBeaconAgent.Server.Core.Services
{
    internal class PairingService : IPairingService
    {
        private readonly IBeaconServerIdentity mIdentity;
        private readonly ILogger<PairingService> mLogger;

        private string mPin = string.Empty;
        private DateTime mPinExpiry;
        private bool mPinUsed;
        private int mFailedAttempts;

        // Guards all mutable pairing state (mPin, mPinExpiry, mPinUsed,
        // mFailedAttempts). PairingService is a singleton, and /api/pair
        // can be called concurrently — without this lock two simultaneous
        // requests could both read mPinUsed == false, both validate the
        // PIN, and both return the ApiKey (PIN not single-use), or lose
        // the failed-attempt counter increments.
        private readonly Lock mStateLock = new();

        // Maximum failed PIN attempts before pairing locks out.
        // Prevents brute-forcing 10^6 combinations over the LAN.
        private const int MaxFailedAttempts = 5;

        // PIN validity window. Short enough to limit exposure,
        // long enough for the user to read it from the console.
        private static readonly TimeSpan PinLifetime = TimeSpan.FromMinutes(5);

        public bool IsPairingActive => !string.IsNullOrEmpty(mPin) &&
                                       !mPinUsed &&
                                       mFailedAttempts < MaxFailedAttempts &&
                                       DateTime.UtcNow < mPinExpiry;

        public event Action<PairingStateEventArgs>? PairingStateChanged;

        public PairingService(IBeaconServerIdentity identity, ILogger<PairingService> logger)
        {
            mIdentity = identity;
            mLogger = logger;
        }

        

        /// <inheritdoc />
        public string? ValidateAndExchangePin(string pin)
        {
            lock (mStateLock)
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

                PairingStateChanged?.Invoke(new PairingStateEventArgs
                {
                    State = PairingState.Used
                });

                return mIdentity.ApiKey;
            }
        }

        /// <inheritdoc />
        public void RegeneratePin()
        {
            lock (mStateLock)
            {
                GeneratePin();
            }
        }

        /// <inheritdoc />
        public string GetCurrentPin()
        {
            lock (mStateLock)
            {
                return IsPairingActive ? mPin : string.Empty;
            }
        }

        private void GeneratePin()
        {
            // 6 digits: 100 000–999 999, easy to type on a phone keyboard.
            // RandomNumberGenerator is a CSPRNG — Random.Shared (xoshiro)
            // is not, and while the 5-attempt lockout makes brute-force
            // impractical anyway, using a CSPRNG removes any doubt about
            // predictability of the next PIN from observed previous PINs.
            mPin = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();
            mPinExpiry = DateTime.UtcNow.Add(PinLifetime);
            mPinUsed = false;
            mFailedAttempts = 0;

            // Prominent separator makes the PIN easy to spot in a scrolling log.
            LogNewPin(mPin, (int)PinLifetime.TotalMinutes);

            // Notify subscribers (tray host) that a new PIN is available.
            PairingStateChanged?.Invoke(new PairingStateEventArgs
            {
                State = PairingState.Generated,
                Pin = mPin,
                ExpiryUtc = mPinExpiry
            });

            // Schedule an expiry notification. If the PIN is not used within
            // the lifetime window, fire the Expired event so the tray can
            // hide the balloon.
            _ = Task.Delay(PinLifetime).ContinueWith(_ =>
            {
                lock (mStateLock)
                {
                    if (!mPinUsed && DateTime.UtcNow >= mPinExpiry)
                    {
                        PairingStateChanged?.Invoke(new PairingStateEventArgs
                        {
                            State = PairingState.Expired
                        });
                    }
                }
            });
        }

        #region Structured logging definitions (allocation-free)

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

        #endregion
    }
}