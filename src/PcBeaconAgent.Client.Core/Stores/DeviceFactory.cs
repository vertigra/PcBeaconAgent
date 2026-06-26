using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models.Client;
using PcBeaconAgent.Client.Core.Models.Common;
using PcBeaconAgent.Client.Core.Services;
using System.Net.Http;

namespace PcBeaconAgent.Client.Core.Stores
{
    public class DeviceFactory(IHttpClientFactory mHttpClientFactory, IPreferencesService mPrefs)
    {
        public ManagedDevice Create(BeaconDevice beacon)
        {
            return new ManagedDevice(
                beacon,
                new AudioController(beacon.IpAddress, beacon.ApiPort, mPrefs, mHttpClientFactory.CreateClient()),
                new DisplayController(beacon.IpAddress, beacon.ApiPort, mHttpClientFactory.CreateClient())
            );
        }
    }
}