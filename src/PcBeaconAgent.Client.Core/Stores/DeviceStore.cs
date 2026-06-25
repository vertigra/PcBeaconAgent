using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models.Client;
using PcBeaconAgent.Client.Core.Models.Common;
using System.Collections.ObjectModel;
using System.Linq;

namespace PcBeaconAgent.Client.Core.Stores;

public class DeviceStore
{
    private readonly DeviceFactory mFactory;
    private readonly IDeviceStorageService mStorage;
    public ObservableCollection<ManagedDevice> ManagedDevices { get; } = [];

    public DeviceStore(DeviceFactory factory, IDeviceStorageService storage)
    {
        mFactory = factory;
        mStorage = storage;

        LoadRememberDevices();
    }

    public ManagedDevice RememberDevice(BeaconDevice device)
    {
        var existing = ManagedDevices.FirstOrDefault(m => m.Device.Equals(device));
        if (existing != null)
            return existing;

        var managed = mFactory.Create(device);
        ManagedDevices.Add(managed);
        Persist(); 

        return managed;
    }

    public void ForgetDevice(BeaconDevice device)
    {
        var exiting = ManagedDevices.FirstOrDefault(m => m.Device.Equals(device));
        if (exiting != null)
        {
            ManagedDevices.Remove(exiting);
            Persist();
        }
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

    private void Persist() => mStorage.SaveDevices(ManagedDevices.Select(m => m.Device));
}