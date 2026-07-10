using System;
using System.Threading;

namespace PcBeaconAgent.Server.Core.Services
{
    /// <summary>
    /// Acquires a named system-wide mutex so that only one PcBeaconAgent
    /// host process (either <c>Server.Cli</c> or <c>Server.Tray</c>) can
    /// run at a time on the machine. Both hosts bind the same UDP
    /// discovery port and the same HTTP port, so a second instance would
    /// crash on <c>AddressAlreadyInUse</c> — the mutex converts that
    /// obscure socket error into a clear log message and a clean exit.
    /// </summary>
    /// <remarks>
    /// <b>Lifetime.</b> The mutex is held for the entire process
    /// lifetime. Dispose it only at shutdown — releasing earlier allows
    /// a second instance to start while the first is still running.
    /// <para>
    /// <b>Scope.</b> The mutex is global (<c>Global\</c> prefix) so it
    /// constrains across user sessions and Windows Service contexts.
    /// A <c>Local\</c> mutex would only prevent duplicates within the
    /// same session, which is not what we want — a user logged in
    /// interactively could start Tray while a Windows Service runs Cli
    /// under a different account.
    /// </para>
    /// <para>
    /// <b>Abandoned mutex.</b> If a previous instance crashed without
    /// releasing the mutex, <see cref="Mutex.OpenExisting"/> would
    /// throw <see cref="AbandonedMutexException"/> — but
    /// <see cref="Mutex(Boolean, String, out Boolean)"/> handles this
    /// transparently and returns ownership to us. We do not need to
    /// special-case it.
    /// </para>
    /// </remarks>
    public sealed class SingleInstanceGuard : IDisposable
    {
        /// <summary>
        /// Mutex name. The <c>Global\</c> prefix makes the mutex visible
        /// across all user sessions on the machine — required because
        /// Cli can run as a Windows Service (LocalSystem) while Tray
        /// runs in the interactive user's session.
        /// </summary>
        public const string MutexName = @"Global\PcBeaconAgent-SingleInstance";

        private Mutex? mMutex;
        private bool mOwned;
        private bool mDisposed;

        /// <summary>
        /// <c>true</c> after a successful <see cref="TryAcquire"/> call
        /// — the caller owns the mutex and the process may continue
        /// startup. <c>false</c> if another instance is already running.
        /// </summary>
        public bool IsOwned => mOwned;

        /// <summary>
        /// Attempts to acquire the single-instance mutex. Returns
        /// <c>false</c> if another PcBeaconAgent process is already
        /// running — the caller should log a clear message and exit.
        /// </summary>
        /// <returns>
        /// <c>true</c> if this process now owns the mutex and may
        /// continue; <c>false</c> if another instance is already
        /// running.
        /// </returns>
        public bool TryAcquire()
        {
            if (mOwned) return true;
            if (mDisposed) throw new ObjectDisposedException(nameof(SingleInstanceGuard));

            // createOwnership: true → create the mutex if it does not
            // exist. owned: receives true if the calling thread was
            // granted initial ownership, false if another process
            // already owns it.
            mMutex = new Mutex(initiallyOwned: true, name: MutexName, createdNew: out mOwned);
            return mOwned;
        }

        public void Dispose()
        {
            if (mDisposed) return;
            mDisposed = true;

            if (mMutex != null)
            {
                try
                {
                    if (mOwned)
                    {
                        // ReleaseMutex must be called from the same
                        // thread that called WaitOne / acquired it.
                        // The host process is single-threaded at
                        // disposal time in practice (shutdown runs on
                        // the main thread), so this is safe.
                        mMutex.ReleaseMutex();
                    }
                }
                catch (ApplicationException)
                {
                    // Thrown if the calling thread does not own the
                    // mutex — happens when TryAcquire returned false
                    // but Dispose is still called. Safe to ignore;
                    // we never owned it, so there is nothing to release.
                }
                mMutex.Dispose();
            }
        }
    }
}
