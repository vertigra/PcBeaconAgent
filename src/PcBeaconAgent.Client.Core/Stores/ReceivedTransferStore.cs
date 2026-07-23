using PcBeaconAgent.Client.Core.Models;
using System;
using System.Collections.ObjectModel;

namespace PcBeaconAgent.Client.Core.Stores
{
    /// <summary>
    /// In-memory singleton store for transfers received from the PC
    /// via SignalR push. Bound to the Android Received page's
    /// CollectionView. Cap at 100 items — oldest evicted first.
    /// </summary>
    public class ReceivedTransferStore
    {
        private const int MaxItems = 100;

        /// <summary>
        /// Received transfers, newest first. UI binds directly to this
        /// collection. Mutations must happen on the UI thread.
        /// </summary>
        public ObservableCollection<ReceivedTransfer> Items { get; } = [];

        /// <summary>
        /// Adds a received transfer to the front of the list. If the
        /// list exceeds the cap, the oldest item is removed.
        /// </summary>
        public void Add(ReceivedTransfer item)
        {
            Items.Insert(0, item);
            while (Items.Count > MaxItems)
            {
                Items.RemoveAt(Items.Count - 1);
            }
        }

        public void Clear() => Items.Clear();
    }
}
