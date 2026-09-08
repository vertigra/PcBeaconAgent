using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using PcBeaconAgent.Contracts.Models;
using PcBeaconAgent.Server.Core.Interfaces;
using PcBeaconAgent.Server.Core.Models;
using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PcBeaconAgent.Server.Core.Services
{
    public class BeaconServiceHub(IBeaconServerIdentity mIdentity, ILogger<BeaconServiceHub> mLogger, IConnectionTracker mTracker, TransferController mTransferController) : Hub
    {
        public override async Task OnConnectedAsync()
        {
            if (!IsAuthorized())
            {
                LogUnauthorizedConnection(Context.ConnectionId);
                Context.Abort();
                return;
            }

            LogClientConnected();

            var http = Context.GetHttpContext();
            string? clientIp = http?.Connection.RemoteIpAddress?.ToString();

            mTracker.Register(Context.ConnectionId, new ClientInfo
            {
                RemoteIp = clientIp,
                UserAgent = http?.Request.Headers.UserAgent.ToString(),
                MachineName = http?.Request.Query["machine"].ToString()
            });

            await base.OnConnectedAsync();

            // Replay any pending transfers queued while this client
            // was offline. Fire-and-forget — the replay runs on the
            // hub thread pool; if it fails (client disconnects
            // mid-replay), the items are re-queued by the controller.
            if (!string.IsNullOrEmpty(clientIp))
            {
                _ = mTransferController.ReplayPendingTransfers(Context.ConnectionId, clientIp);
            }
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            LogClientDisconnected(exception?.Message);
            mTracker.Unregister(Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }

        public BeaconDevice GetDeviceDetails() => GetLocalDeviceData();

        private bool IsAuthorized()
        {
            // Fail-closed: empty ApiKey means auth is required but no key
            // is configured — reject all connections. To allow anonymous
            // connections, set ApiKey to a non-empty value AND add
            // "AllowAnonymous": true in appsettings.json (future).
            if (string.IsNullOrWhiteSpace(mIdentity.ApiKey))
                return false;

            var http = Context.GetHttpContext();
            string? provided = http?.Request.Headers["X-Api-Key"];

            // Query-string fallback for SignalR WebSocket handshake.
            // SignalR clients on Android cannot set custom headers on
            // the WebSocket upgrade request — the key must go in the
            // query string. This is the standard SignalR auth pattern.
            if (string.IsNullOrEmpty(provided))
            {
                provided = http?.Request.Query["api_key"];
            }

            if (string.IsNullOrEmpty(provided))
                return false;

            // Constant-time comparison to prevent timing attacks.
            return FixedTimeEquals(provided, mIdentity.ApiKey);
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            byte[] aBytes = Encoding.UTF8.GetBytes(a);
            byte[] bBytes = Encoding.UTF8.GetBytes(b);
            return aBytes.Length == bBytes.Length &&
                   CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
        }

        private BeaconDevice GetLocalDeviceData()
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            var activeInterface = networkInterfaces
                .FirstOrDefault(ni => ni.OperationalStatus == OperationalStatus.Up &&
                    (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                     ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211));

            return new BeaconDevice
            {
                MachineName = Environment.MachineName,
                ApiPort = mIdentity.ApiPort,
                MacAddress = activeInterface?.GetPhysicalAddress().ToString() ?? "00:00:00:00:00:00",
                InterfaceName = activeInterface?.Name ?? "Unknown",
                InterfaceType = activeInterface?.NetworkInterfaceType.ToString() ?? "Unknown",
            };
        }

        #region Structured logging definitions (allocation-free)

        private static readonly Action<ILogger, string, Exception?> LogUnauthorizedAction =
            LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1, "Unauthorized"),
                "Rejected hub connection {ConnectionId}: invalid or missing API key");

        private static readonly Action<ILogger, Exception?> LogConnectedAction =
            LoggerMessage.Define(LogLevel.Information, new EventId(2, "Connected"),
                "Client connected");

        private static readonly Action<ILogger, string?, Exception?> LogDisconnectedAction =
            LoggerMessage.Define<string?>(LogLevel.Information, new EventId(3, "Disconnected"),
                "Client disconnected by reason {reason}");

        private void LogUnauthorizedConnection(string id) => LogUnauthorizedAction(mLogger, id, null);
        private void LogClientConnected() => LogConnectedAction(mLogger, null);
        private void LogClientDisconnected(string? reason) => LogDisconnectedAction(mLogger, reason, null);

        #endregion
    }
}
