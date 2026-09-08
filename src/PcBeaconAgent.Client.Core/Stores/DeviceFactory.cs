using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models;
using PcBeaconAgent.Client.Core.Services;
using PcBeaconAgent.Contracts.Models;
using System.Net.Http;

namespace PcBeaconAgent.Client.Core.Stores
{
    public class DeviceFactory(IHttpClientFactory mHttpClientFactory, IPreferencesService mPrefs)
    {
        public ManagedDevice Create(BeaconDevice beacon)
        {
            return new ManagedDevice(beacon,
                new AudioServiceClient(beacon.IpAddress, beacon.ApiPort, mPrefs, mHttpClientFactory.CreateClient()),
                new DisplayServiceClient(beacon.IpAddress, beacon.ApiPort, mPrefs, mHttpClientFactory.CreateClient()),
                new TransferServiceClient(beacon.IpAddress, beacon.ApiPort, mPrefs, mHttpClientFactory.CreateClient()),
                new LauncherServiceClient(beacon.IpAddress, beacon.ApiPort, mPrefs, mHttpClientFactory.CreateClient())
            );
        }
    }
}