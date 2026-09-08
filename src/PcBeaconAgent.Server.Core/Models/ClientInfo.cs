namespace PcBeaconAgent.Server.Core.Models
{
    /// <summary>
    /// Information about a connected client. Stored as the value in
    /// <see cref="Interfaces.IConnectionTracker"/>'s dictionary, keyed
    /// by SignalR connection ID. Keeps enough context to identify a
    /// client without holding a reference to the SignalR connection
    /// itself.
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

        /// <summary>
        /// Machine name of the connected client, if the client provided
        /// it via the <c>machine</c> query-string parameter on the
        /// SignalR handshake. Used by the tray UI to display a
        /// human-readable label for the connected device (e.g. when
        /// choosing a recipient for an outgoing transfer). Empty if the
        /// client did not send it — the UI falls back to the IP address.
        /// </summary>
        public string? MachineName { get; init; }
    }
}
