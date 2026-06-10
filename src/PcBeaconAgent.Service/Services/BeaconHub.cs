using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PcBeaconAgent.Client.Core.Models;
using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace PcBeaconAgent.Service.Services
{
    public class BeaconHub(IConfiguration mConfig, ILogger<BeaconHub> mLogger) : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var deviceData = GetLocalDeviceData();
            await Clients.Caller.SendAsync("ReceiveDeviceDetails", deviceData);
            await Clients.Caller.SendAsync("CloseConnection");
            await base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            mLogger.LogInformation("Client disconnected by reason {reason}", exception?.Message);
            return base.OnDisconnectedAsync(exception);
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
                ApiPort = mConfig.GetValue<int>("Server:ApiPort"),
                MacAddress = activeInterface?.GetPhysicalAddress().ToString() ?? "00:00:00:00:00:00",
                InterfaceName = activeInterface?.Name ?? "Unknown",
                InterfaceType = activeInterface?.NetworkInterfaceType.ToString() ?? "Unknown",
            };
        }
    }
}
