using PcBeaconAgent.Client.Core.Models;
using PcBeaconAgent.Client.Core.Models.Common;
using System.Collections.Generic;

namespace PcBeaconAgent.Client.Core.Interfaces
{
    public interface IDeviceStorageService
    {
        void SaveDevices(IEnumerable<BeaconDevice> devices);
        IEnumerable<BeaconDevice> LoadDevices();
    }
}
