using PcBeaconAgent.Server.Core.Interfaces;
using PcBeaconAgent.Server.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;

namespace PcBeaconAgent.Server.Core.Services
{
    /// <summary>
    /// Implementation of <see cref="IConnectionTracker"/>. Thread-safe
    /// via a <see cref="Lock"/> around the dictionary. Event raises
    /// are marshaled onto the <see cref="SynchronizationContext"/>
    /// captured at construction time.
    /// </summary>
    internal sealed class ConnectionTracker : IConnectionTracker
    {
        // Guards mClients. SignalR hub methods run on thread-pool
        // threads, and multiple connections can connect/disconnect
        // concurrently.
        private readonly Lock mLock = new();
        private readonly Dictionary<string, ClientInfo> mClients = new();
        private readonly SynchronizationContext? mSyncContext;

        public ConnectionTracker()
        {
            // Capture the current sync context. In the tray host this
            // is the WPF Dispatcher sync context (the App runs on the
            // UI thread). In the CLI host it is null — events fire on
            // the hub's thread-pool thread.
            mSyncContext = SynchronizationContext.Current;
        }

        public int ConnectedCount
        {
            get
            {
                lock (mLock) { return mClients.Count; }
            }
        }

        public IReadOnlyDictionary<string, ClientInfo> ConnectedClients
        {
            get
            {
                lock (mLock)
                {
                    // Return a snapshot copy so the caller can iterate
                    // without holding the lock and without seeing
                    // concurrent mutations.
                    return new Dictionary<string, ClientInfo>(mClients);
                }
            }
        }

        public event Action<int>? CountChanged;

        public void Register(string connectionId, ClientInfo info)
        {
            int newCount;
            lock (mLock)
            {
                mClients[connectionId] = info;
                newCount = mClients.Count;
            }
            RaiseCountChanged(newCount);
        }

        public void Unregister(string connectionId)
        {
            int newCount;
            lock (mLock)
            {
                mClients.Remove(connectionId);
                newCount = mClients.Count;
            }
            RaiseCountChanged(newCount);
        }

        private void RaiseCountChanged(int newCount)
        {
            var handler = CountChanged;
            if (handler == null) return;

            if (mSyncContext != null)
            {
                // Marshal to the captured context (WPF Dispatcher in
                // the tray host). Post is asynchronous — the hub
                // thread is not blocked.
                mSyncContext.Post(_ => handler(newCount), null);
            }
            else
            {
                // No sync context (CLI host) — fire on the calling
                // thread. Subscribers that touch UI must marshal.
                handler(newCount);
            }
        }
    }
}
