using System.Collections.Generic;

namespace PcBeaconAgent.Client.Core.Interfaces
{
    public interface IDeviceStorageService
    {
        void SaveDevices(IEnumerable<BeaconDevice> devices);
        IEnumerable<BeaconDevice> LoadDevices();
    }
}
