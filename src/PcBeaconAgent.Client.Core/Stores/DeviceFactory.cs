using PcBeaconAgent.Client.Core.Models;
using PcBeaconAgent.Client.Core.Services;
using System.Net.Http;

namespace PcBeaconAgent.Client.Core.Stores
{
    public class DeviceFactory
    {
        private readonly IHttpClientFactory mHttpClientFactory;
        public DeviceFactory(IHttpClientFactory httpClientFactory)
        {
            mHttpClientFactory = httpClientFactory;
        }

        public ManagedDevice Create(BeaconDevice beacon)
        {
            return new ManagedDevice(
                beacon,
                new AudioController(beacon.IpAddress, beacon.ApiPort, mHttpClientFactory.CreateClient()),
                new MonitorController(beacon.IpAddress, beacon.ApiPort, mHttpClientFactory.CreateClient())
            );
        }
    }
}
