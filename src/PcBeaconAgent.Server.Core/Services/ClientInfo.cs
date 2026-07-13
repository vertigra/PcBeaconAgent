using PcBeaconAgent.Server.Core.Interfaces;

namespace PcBeaconAgent.Server.Core.Services
{
    /// <summary>
    /// Information about a connected client. Stored as the value in
    /// <see cref="IConnectionTracker"/>'s dictionary, keyed by SignalR
    /// connection ID. Keeps enough context to identify a client without
    /// holding a reference to the SignalR connection itself.
    /// </summary>
    public sealed class ClientInfo
    {
        /// <summary>
        /// IP address of the connected client, or <c>null</c> if the
        /// connection has no HTTP context (should not happen in normal
        /// operation — SignalR always wraps in an HTTP request).
        /// </summary>
        public string? RemoteIp { get; init; }

        /// <summary>
        /// Optional user-agent string from the HTTP headers. Useful for
        /// distinguishing client app versions or platforms.
        /// </summary>
        public string? UserAgent { get; init; }
    }
}
