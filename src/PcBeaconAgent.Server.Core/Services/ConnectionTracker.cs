using PcBeaconAgent.Server.Core.Interfaces;
using PcBeaconAgent.Server.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace PcBeaconAgent.Server.Core.Services
{
    /// <summary>
    /// Implementation of <see cref="IConnectionTracker"/>. Thread-safe
    /// via a <see cref="Lock"/> around the dictionaries. Event raises
    /// are marshaled onto the <see cref="SynchronizationContext"/>
    /// captured at construction time.
    /// </summary>
    internal sealed class ConnectionTracker : IConnectionTracker
    {
        private readonly Lock mLock = new();
        private readonly Dictionary<string, ClientInfo> mClients = new();
        private readonly Dictionary<string, ClientInfo> mKnownClients = new();
        private readonly SynchronizationContext? mSyncContext;

        public ConnectionTracker()
        {
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
                    return new Dictionary<string, ClientInfo>(mClients);
                }
            }
        }

        public IReadOnlyDictionary<string, ClientInfo> KnownClients
        {
            get
            {
                lock (mLock)
                {
                    return new Dictionary<string, ClientInfo>(mKnownClients);
                }
            }
        }

        public string? FindConnectionIdByIp(string ip)
        {
            if (string.IsNullOrEmpty(ip)) return null;
            lock (mLock)
            {
                return mClients.FirstOrDefault(kvp =>
                    kvp.Value.RemoteIp == ip).Key;
            }
        }

        public event Action<int>? CountChanged;

        public void Register(string connectionId, ClientInfo info)
        {
            int newCount;
            lock (mLock)
            {
                mClients[connectionId] = info;

                // Persist in KnownClients keyed by IP — survives
                // disconnect so the tray UI can show the device even
                // when offline, and TransferController can queue
                // pending transfers for it.
                if (!string.IsNullOrEmpty(info.RemoteIp))
                {
                    mKnownClients[info.RemoteIp] = info;
                }

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
                // Do NOT remove from mKnownClients — the device stays
                // visible in the tray UI as "offline" so the user can
                // queue transfers.
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
                mSyncContext.Post(_ => handler(newCount), null);
            }
            else
            {
                handler(newCount);
            }
        }
    }
}
