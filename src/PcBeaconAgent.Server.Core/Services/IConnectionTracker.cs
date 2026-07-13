using System;
using System.Collections.Generic;

namespace PcBeaconAgent.Server.Core.Services
{
    /// <summary>
    /// Tracks SignalR connections to <see cref="BeaconServiceHub"/>.
    /// Singleton — survives across hub instances (the hub is transient,
    /// created per connection, so it cannot hold state itself).
    /// </summary>
    /// <remarks>
    /// <b>Threading.</b> The tracker captures a
    /// <see cref="System.Threading.SynchronizationContext"/> at
    /// construction time and marshals all event raises onto that
    /// context. When the tray host constructs the tracker, the
    /// SynchronizationContext is the WPF Dispatcher sync context, so
    /// <see cref="CountChanged"/> fires on the UI thread and view
    /// models can update their observable properties directly. When
    /// the CLI host constructs the tracker, there is no sync context
    /// (null) — the event fires on the calling thread and any
    /// subscriber that touches UI must marshal itself.
    /// </remarks>
    public interface IConnectionTracker
    {
        /// <summary>
        /// Current number of authorised connected clients. Read-only
        /// from the consumer's perspective — the hub updates it via
        /// <see cref="Register"/> / <see cref="Unregister"/>.
        /// </summary>
        int ConnectedCount { get; }

        /// <summary>
        /// Read-only snapshot of the connected clients, keyed by
        /// SignalR connection ID. Useful for future UI surfaces that
        /// list individual connections. Do not mutate the returned
        /// dictionary — it is a snapshot copy.
        /// </summary>
        IReadOnlyDictionary<string, ClientInfo> ConnectedClients { get; }

        /// <summary>
        /// Raised on the captured <see cref="SynchronizationContext"/>
        /// whenever a client connects or disconnects. The int parameter
        /// is the new <see cref="ConnectedCount"/>.
        /// </summary>
        event Action<int>? CountChanged;

        /// <summary>
        /// Called by <see cref="BeaconServiceHub.OnConnectedAsync"/>
        /// after the connection passes authorisation. Registers the
        /// client and raises <see cref="CountChanged"/>.
        /// </summary>
        void Register(string connectionId, ClientInfo info);

        /// <summary>
        /// Called by <see cref="BeaconServiceHub.OnDisconnectedAsync"/>.
        /// Removes the client and raises <see cref="CountChanged"/>.
        /// No-op if the connection ID is not registered (e.g. the
        /// connection was rejected before authorisation).
        /// </summary>
        void Unregister(string connectionId);
    }
}
