using Microsoft.Maui;
using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models;
using PcBeaconAgent.Client.Core.Stores;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Android;

public partial class App : Application
{
    private readonly ISignalService mSignalService;
    private readonly DeviceStore mDeviceStore;
    public ObservableCollection<ManagedDevice> ManagedDevices => mDeviceStore.ManagedDevices;

    public App(DeviceStore store, ISignalService signalRService)
    {
        InitializeComponent();

        mSignalService = signalRService;
        mDeviceStore = store;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    protected override void OnStart()
    {
        Task.Run(ConnectToManagedDevices);
        base.OnStart();
    }

    protected override void OnSleep()
    {
        foreach (var device in ManagedDevices.Select(x => x.Device))
        {
            mSignalService.DisconnectBeaconHubAsync(device);
        }

        base.OnSleep();
    }

    protected override void OnResume()
    {
        Task.Run(ConnectToManagedDevices);
        base.OnResume();
    }

    private void ConnectToManagedDevices()
    {
        foreach(var device in ManagedDevices.Select(x=>x.Device))
        {
            mSignalService.ConnectToBeaconHubAsync(device);
        }
    }
}