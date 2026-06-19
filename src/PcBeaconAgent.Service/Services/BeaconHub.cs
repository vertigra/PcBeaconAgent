using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using PcBeaconAgent.Client.Core.Models;
using PcBeaconAgent.Service.Interfaces;
using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace PcBeaconAgent.Service.Services
{
    public class BeaconHub(IBeaconAnnouncementService mBeaconService, ILogger<BeaconHub> mLogger) : Hub
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
            await base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            LogClientDisconnected(exception?.Message);
            return base.OnDisconnectedAsync(exception);
        }

        public async Task ReceiveDeviceDetailsAndClose()
        {
            var deviceData = GetLocalDeviceData();
            await Clients.Caller.SendAsync("ReceiveDeviceDetails", deviceData);
            await Clients.Caller.SendAsync("CloseConnection");
        }

        private bool IsAuthorized()
        {
            if (string.IsNullOrEmpty(mBeaconService.ApiKey))
                return true;

            var http = Context.GetHttpContext();
            string? provided = http?.Request.Headers["X-Api-Key"];
            if (string.IsNullOrEmpty(provided))
                provided = http?.Request.Query["api_key"];

            return string.Equals(provided, mBeaconService.ApiKey, StringComparison.Ordinal);
        }

        private BeaconDevice GetLocalDeviceData()
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            var activeInterface = networkInterfaces
                .FirstOrDefault(ni =>
                    ni.OperationalStatus == OperationalStatus.Up &&
                    (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                     ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211));

            return new BeaconDevice
            {
                MachineName = Environment.MachineName,
                ApiPort = mBeaconService.ApiPort,
                MacAddress = activeInterface?.GetPhysicalAddress().ToString()
                             ?? "00:00:00:00:00:00",
                InterfaceName = activeInterface?.Name ?? "Unknown",
                InterfaceType = activeInterface?.NetworkInterfaceType.ToString() ?? "Unknown",
            };
        }

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
    }
}