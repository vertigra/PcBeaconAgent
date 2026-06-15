using PcBeaconAgent.Client.Core.Models;
using PcBeaconAgent.Client.Core.Services;
using System.Net.Http;

namespace PcBeaconAgent.Client.Core.Stores
{
    public class DeviceFactory
    {
        private readonly IHttpClientFactory _httpClientFactory;
        public DeviceFactory(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

        public ManagedDevice Create(BeaconDevice beacon) => 
            new(beacon, 
            new AudioController(beacon.IpAddress, beacon.ApiPort, _httpClientFactory.CreateClient()),
            new MonitorController(beacon.IpAddress, beacon.ApiPort, _httpClientFactory.CreateClient())
        );
    }
}
