using System;
using System.Net;

namespace PcBeaconAgent.Client.Core.Exceptions
{
    /// <summary>
    /// Thrown by <see cref="Services.PairingServiceClient"/> when the server
    /// returns a non-success status code. Carries the server's explanation
    /// (read from the <c>MessageDto</c> body) and the HTTP status code so the
    /// ViewModel can map specific codes (401 wrong PIN, 403 pairing inactive)
    /// to user-facing messages.
    /// </summary>
    public class PairingHttpException : Exception
    {
        public HttpStatusCode StatusCode { get; }

        public PairingHttpException(string message, HttpStatusCode statusCode)
            : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
