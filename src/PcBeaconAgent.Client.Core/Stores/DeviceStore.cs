using PcBeaconAgent.Client.Core.Models;
using System.Collections.ObjectModel;
using System.Linq;

namespace PcBeaconAgent.Client.Core.Stores
{
    public class DeviceStore(DeviceFactory mFactory)
    {
        public ObservableCollection<ManagedDevice> ManagedDevices { get; } = [];

        public void Connect(BeaconDevice beacon)
        {
            if (ManagedDevices.Any(m => m.Device.Equals(beacon))) 
                return;

            ManagedDevices.Add(mFactory.Create(beacon));
        }
    }
}
