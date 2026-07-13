using Microsoft.Extensions.Logging;
using PcBeaconAgent.Server.Core.Events;
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

        // Monotonic counter incremented on every successful GeneratePin call.
        // Used by the delayed Expired continuation to detect that a newer PIN
        // has superseded the one it was scheduled for — without this, regenerating
        // a PIN would still fire Expired for the old one when its timer elapses.
        private long mPinGeneration;

        // Cancellable handle for the pending Expired event. Cancelled when the
        // PIN is used or regenerated — so the delayed continuation becomes a
        // no-op instead of firing a spurious Expired.
        private CancellationTokenSource? mExpiryCts;

        // Guards all mutable pairing state (mPin, mPinExpiry, mPinUsed,
        // mFailedAttempts, mPinGeneration, mExpiryCts). PairingService is a
        // singleton, and /api/pair can be called concurrently — without this
        // lock two simultaneous requests could both read mPinUsed == false,
        // both validate the PIN, and both return the ApiKey (PIN not
        // single-use), or lose the failed-attempt counter increments.
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
            PairingState? stateToRaise = null;
            string? apiKey = null;
            bool reject = false;

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
                    {
                        LogInvalidPin(remaining);
                    }
                    else
                    {
                        LogPairingLocked();
                        mExpiryCts?.Cancel();
                        stateToRaise = PairingState.Locked;
                    }

                    reject = true;
                }
                else
                {
                    // PIN is correct — single-use: invalidate immediately.
                    mPinUsed = true;
                    mExpiryCts?.Cancel();
                    LogPairingSuccess();
                    stateToRaise = PairingState.Used;
                    apiKey = mIdentity.ApiKey;
                }
            }

            // Raise events OUTSIDE the lock. The event handler calls
            // Dispatcher.BeginInvoke → GetCurrentPin (which acquires
            // the lock). If we raised inside the lock, the UI thread
            // would block on GetCurrentPin while the HTTP thread blocks
            // on the event — classic deadlock.
            if (stateToRaise.HasValue)
            {
                PairingStateChanged?.Invoke(new PairingStateEventArgs
                {
                    State = stateToRaise.Value
                });
            }

            return reject ? null : apiKey;
        }

        /// <inheritdoc />
        public void RegeneratePin()
        {
            // GeneratePin manages its own locking internally — it
            // acquires mStateLock, generates the PIN, releases the
            // lock, THEN raises the Generated event. Do NOT wrap
            // GeneratePin in an outer lock here: the event is raised
            // outside GeneratePin's lock scope, and if an outer lock
            // were held, the event handler (which calls Dispatcher.BeginInvoke
            // and then GetCurrentPin → lock) would deadlock.
            GeneratePin();
        }

        /// <inheritdoc />
        public string GetCurrentPin()
        {
            lock (mStateLock)
            {
                return IsPairingActive ? mPin : string.Empty;
            }
        }

        /// <inheritdoc />
        public DateTime? GetCurrentPinExpiryUtc()
        {
            lock (mStateLock)
            {
                return IsPairingActive ? mPinExpiry : null;
            }
        }

        private void GeneratePin()
        {
            string pin;
            DateTime expiry;
            long generation;
            CancellationTokenSource cts;

            lock (mStateLock)
            {
                // Cancel any pending Expired event from a previous PIN.
                // The continuation's generation check would also catch this,
                // but cancelling avoids the extra Task.Delay wakeup.
                mExpiryCts?.Cancel();
                mExpiryCts?.Dispose();
                mExpiryCts = new CancellationTokenSource();
                cts = mExpiryCts;

                // 6 digits: 100 000–999 999, easy to type on a phone keyboard.
                // RandomNumberGenerator is a CSPRNG — Random.Shared (xoshiro)
                // is not, and while the 5-attempt lockout makes brute-force
                // impractical anyway, using a CSPRNG removes any doubt about
                // predictability of the next PIN from observed previous PINs.
                mPin = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();
                mPinExpiry = DateTime.UtcNow.Add(PinLifetime);
                mPinUsed = false;
                mFailedAttempts = 0;
                generation = ++mPinGeneration;

                pin = mPin;
                expiry = mPinExpiry;

                // Prominent separator makes the PIN easy to spot in a scrolling log.
                LogNewPin(mPin, (int)PinLifetime.TotalMinutes);
            }

            // Notify subscribers (tray host) that a new PIN is available.
            PairingStateChanged?.Invoke(new PairingStateEventArgs
            {
                State = PairingState.Generated,
                Pin = pin,
                ExpiryUtc = expiry
            });

            // Schedule Expired. The continuation re-checks both the generation
            // counter (a newer PIN may have superseded this one) and mPinUsed
            // (the PIN may have been exchanged). Only if neither has happened
            // does it fire Expired. Cancellation is the fast path — it avoids
            // the extra Task.Delay wakeup when the PIN is regenerated or used.
            CancellationToken token = cts.Token;
            _ = Task.Delay(PinLifetime, token).ContinueWith(t =>
            {
                if (t.IsCanceled) return;

                bool shouldFire;
                lock (mStateLock)
                {
                    shouldFire = mPinGeneration == generation && !mPinUsed;
                }

                if (shouldFire)
                {
                    LogPinExpired();
                    PairingStateChanged?.Invoke(new PairingStateEventArgs
                    {
                        State = PairingState.Expired
                    });
                }
            }, TaskScheduler.Default);
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

        private static readonly Action<ILogger, Exception?> LogPinExpiredAction =
            LoggerMessage.Define(
                LogLevel.Information,
                new EventId(15, "PairingPinExpired"),
                "Pairing PIN expired without being used. Run RegeneratePin (or reopen the pairing page on the client) to issue a new one.");

        private void LogNewPin(string pin, int minutes) => LogNewPinAction(mLogger, pin, minutes, null);
        private void LogInvalidPin(int remaining) => LogInvalidPinAction(mLogger, remaining, null);
        private void LogPairingLocked() => LogPairingLockedAction(mLogger, null);
        private void LogPairingInactive() => LogPairingInactiveAction(mLogger, null);
        private void LogPairingSuccess() => LogPairingSuccessAction(mLogger, null);
        private void LogPinExpired() => LogPinExpiredAction(mLogger, null);

        #endregion
    }
}