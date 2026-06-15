using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models;
using System.Collections.ObjectModel;
using System.Linq;

namespace PcBeaconAgent.Client.Core.Stores
{
    public class DeviceStore
    {
        private readonly DeviceFactory mFactory;
        private readonly IDeviceStorageService mStorage;
        public ObservableCollection<ManagedDevice> ManagedDevices { get; } = new();

        public DeviceStore(DeviceFactory factory, IDeviceStorageService storage)
        {
            mFactory = factory;
            mStorage = storage;

            LoadRememberDevices();
        }

        private void LoadRememberDevices()
        {
            var saved = mStorage.LoadDevices();
            foreach (var beacon in saved)
            {
                if (!ManagedDevices.Any(m => m.Device.Equals(beacon)))
                    ManagedDevices.Add(mFactory.Create(beacon));
            }
        }

        public void RememberDevice(BeaconDevice beacon)
        {
            if (ManagedDevices.Any(m => m.Device.Equals(beacon))) 
                return;

            ManagedDevices.Add(mFactory.Create(beacon));
            mStorage.SaveDevices(ManagedDevices.Select(m => m.Device));
        }

        public void ForgetDevice(BeaconDevice beacon)
        {
            var device = ManagedDevices.FirstOrDefault(m => m.Device.Equals(beacon));
            if (device != null)
            {
                ManagedDevices.Remove(device);
                mStorage.SaveDevices(ManagedDevices.Select(m => m.Device));
            }
        }
    }
}
