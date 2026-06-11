using PcBeaconAgent.Client.Core.Constants;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models;
using System.Collections.Generic;

namespace PcBeaconAgent.Client.Android.Services
{
    public class DeviceStorageService(IPreferencesService prefs) : IDeviceStorageService
    {
        private readonly IPreferencesService mPrefs = prefs;

        public void SaveDevices(IEnumerable<BeaconDevice> devices) => mPrefs.Set(StorageKeys.KnownDevices, devices);
        public IEnumerable<BeaconDevice> LoadDevices()
            => mPrefs.Get(StorageKeys.KnownDevices, new List<BeaconDevice>()) ?? [];
    }
}
