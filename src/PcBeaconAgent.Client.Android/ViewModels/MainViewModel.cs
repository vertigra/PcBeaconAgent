using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Android.Pages;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models;
using PcBeaconAgent.Client.Core.Stores;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Android.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ISignalService mSignalService;
    private readonly ILogger<MainViewModel> mLogger;
    private readonly DeviceStore mDeviceStore;

    public ObservableCollection<ManagedDevice> ManagedDevices => mDeviceStore.ManagedDevices;

    // HasDevices / HasNoDevices drive the empty-state UI on MainPage.
    // Updated whenever ManagedDevices changes (add/remove/forget) —
    // ObservableCollection raises CollectionChanged, which we hook in
    // the constructor. Both properties notify, so XAML bindings to
    // IsVisible flip the right views in and out.
    [ObservableProperty]
    public partial bool HasDevices { get; set; }

    // Convenience inverse — kept as a separate observable property so
    // XAML can bind directly without needing an InvertedBoolConverter
    // on the empty-state layout.
    [ObservableProperty]
    public partial bool HasNoDevices { get; set; } = true;

    public MainViewModel(
        DeviceStore store,
        ISignalService signalService,
        ILogger<MainViewModel> logger)
    {
        mDeviceStore = store;
        mSignalService = signalService;
        mLogger = logger;

        mSignalService.DeviceStatusChanged += OnDeviceStatusChanged;
        ManagedDevices.CollectionChanged += OnManagedDevicesChanged;
        RefreshHasDevices();
    }

    private void OnDeviceStatusChanged(string ipAddress, bool isOnline)
    {
        var device = ManagedDevices.FirstOrDefault(d => d.Device.IpAddress == ipAddress);
        if (device != null)
        {
            device.IsOnline = isOnline;
        }
    }

    private void OnManagedDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // ObservableCollection does not raise PropertyChanged for Count,
        // so we recompute both boolean properties ourselves. UI thread
        // assumption: DeviceStore mutations happen on the UI thread
        // today (Forget button, signal-service callbacks marshalled by
        // SignalService); if that ever changes, dispatch here.
        RefreshHasDevices();
    }

    private void RefreshHasDevices()
    {
        bool has = ManagedDevices.Count > 0;
        HasDevices = has;
        HasNoDevices = !has;
    }

    [RelayCommand]
    public async Task ManageAudio(ManagedDevice device)
    {
        await Shell.Current.GoToAsync($"{nameof(AudioControlPage)}?ip={device.Device.IpAddress}");
    }

    [RelayCommand]
    public async Task ManageDisplay(ManagedDevice device)
    {
        await Shell.Current.GoToAsync($"{nameof(DisplayControlPage)}?ip={device.Device.IpAddress}");
    }

    [RelayCommand]
    public async Task Forget(ManagedDevice device)
    {
        bool confirm = await Shell.Current.CurrentPage.DisplayAlertAsync("Forget Device",
        $"Forget {device.Device.MachineName}? The pairing key will be removed.",
        "Forget", "Cancel");

        if (!confirm) return;

        await mSignalService.ForgetAsync(device.Device.IpAddress);
        mDeviceStore.ForgetDevice(device.Device);
    }

    [RelayCommand]
    public async Task GoToDiscovery()
    {
        // Shell route names come from AppShell.xaml — DiscoveryPage is
        // the second tab. GoToAsync with the absolute path switches the
        // active tab without pushing a modal.
        await Shell.Current.GoToAsync($"//{nameof(DiscoveryPage)}");
    }

    public void Dispose()
    {
        mSignalService.DeviceStatusChanged -= OnDeviceStatusChanged;
        ManagedDevices.CollectionChanged -= OnManagedDevicesChanged;
    }
}
