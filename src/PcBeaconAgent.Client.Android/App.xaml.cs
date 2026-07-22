using Microsoft.Extensions.Logging;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Android.Pages;
using PcBeaconAgent.Client.Android.ViewModels;
using PcBeaconAgent.Client.Core.Exceptions;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models;
using PcBeaconAgent.Client.Core.Stores;
using PcBeaconAgent.Contracts.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Android;

public partial class App : Application
{
    private readonly ISignalService mSignalService;
    private readonly DeviceStore mDeviceStore;
    private readonly ILogger<App> mLogger;

    public ObservableCollection<ManagedDevice> ManagedDevices => mDeviceStore.ManagedDevices;

    public App(DeviceStore store, ISignalService signalRService, ILogger<App> logger)
    {
        InitializeComponent();
        mSignalService = signalRService;
        mDeviceStore = store;
        mLogger = logger;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    protected override void OnStart()
    {
        _ = Task.Run(ConnectToManagedDevicesAsync);
        // Share-sheet navigation is handled by MainPage.OnAppearing,
        // which is the most reliable trigger point (see comment there).
        base.OnStart();
    }

    protected override void OnSleep()
    {
        // Intentionally do NOT disconnect SignalR connections here.
        // Previously this called DisconnectAllAsync, which dropped
        // every connection when the app went to background. For the
        // share-sheet flow this was catastrophic: each share required
        // a fresh reconnection (1-2 seconds), during which
        // ShareTextPage showed no devices ("offline"). Keeping
        // connections alive in background is fine for a LAN app —
        // SignalR's WithAutomaticReconnect handles transient drops,
        // and OnResume's ConnectToManagedDevicesAsync re-establishes
        // any connection the OS may have killed during long background.
        base.OnSleep();
    }

    protected override void OnResume()
    {
        _ = Task.Run(ConnectToManagedDevicesAsync);
        // Share-sheet navigation is handled by MainPage.OnAppearing.
        base.OnResume();
    }

    private async Task ConnectToManagedDevicesAsync()
    {
        BeaconDevice? firstNotPairedDevice = null;

        foreach (var device in ManagedDevices.Select(x => x.Device).ToList())
        {
            try
            {
                await mSignalService.ConnectToBeaconHubAsync(device);
            }
            catch (NotPairedException ex)
            {
                mLogger.LogWarning(ex, "Device {Ip} is not paired (key missing or invalid)", device.IpAddress);
                firstNotPairedDevice ??= device;
            }
            catch (Exception ex)
            {
                mLogger.LogWarning(ex, "Failed to reconnect to {Ip} on resume", device.IpAddress);
            }
        }

        if (firstNotPairedDevice != null)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                Shell.Current.GoToAsync($"{nameof(PairingPage)}?ip={firstNotPairedDevice.IpAddress}&port={firstNotPairedDevice.ApiPort}"));
        }
    }
}