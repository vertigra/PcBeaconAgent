using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Android.Pages;
using PcBeaconAgent.Client.Core.Exceptions;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Messages;
using PcBeaconAgent.Client.Core.Models.Client;
using PcBeaconAgent.Client.Core.Models.Common;
using PcBeaconAgent.Client.Core.Stores;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Android.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IUdpBeaconScannerService mScanner;
    private readonly ISignalService mSignalService;
    private readonly ILogger<MainViewModel> mLogger;
    private readonly DeviceStore mDeviceStore;

    [ObservableProperty]
    public partial bool IsScanning { get; set; }

    public ObservableCollection<BeaconDevice> DiscoveredDevices { get; } = [];
    public ObservableCollection<ManagedDevice> ManagedDevices => mDeviceStore.ManagedDevices;

    public MainViewModel(
        DeviceStore store,
        IUdpBeaconScannerService scanner,
        ISignalService signalService,
        ILogger<MainViewModel> logger)
    {
        mDeviceStore = store;
        mScanner = scanner;
        mSignalService = signalService;
        mLogger = logger;

        mScanner.OnBeaconFound += OnBeaconFound;
        mSignalService.DeviceStatusChanged += OnDeviceStatusChanged;

        if (!WeakReferenceMessenger.Default.IsRegistered<PairingSucceededMessage>(this))
        {
            WeakReferenceMessenger.Default.Register<PairingSucceededMessage>(this, (recipient, message) =>
            {
                var device = DiscoveredDevices.FirstOrDefault(d => d.IpAddress == message.IpAddress);
                if (device != null)
                    _ = Remember(device);
            });
        }
    }

    private void OnDeviceStatusChanged(string ipAddress, bool isOnline)
    {
        var device = ManagedDevices.FirstOrDefault(d => d.Device.IpAddress == ipAddress);
        if (device != null)
            device.IsOnline = isOnline;
    }

    private void OnBeaconFound(DiscoveredBeacon beacon)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (ManagedDevices.Any(d => d.Device.IpAddress == beacon.IpAddress)) return;
            if (DiscoveredDevices.Any(d => d.IpAddress == beacon.IpAddress)) return;

            var newDevice = new BeaconDevice { IpAddress = beacon.IpAddress, ApiPort = beacon.Port };
            DiscoveredDevices.Add(newDevice);
        });
    }

    private static void UpdateDeviceInfo(BeaconDevice target, BeaconDevice source)
    {
        target.MachineName = source.MachineName;
        target.MacAddress = source.MacAddress;
        target.InterfaceType = source.InterfaceType;
        target.InterfaceName = source.InterfaceName;
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
    public async Task StartScanAsync()
    {
        if (IsScanning) return;

        IsScanning = true;
        DiscoveredDevices.Clear();

        try
        {
            await mScanner.ScanAsync(3000);
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    public async Task Remember(BeaconDevice device)
    {
        try
        {
            var details = await mSignalService.ConnectAndFetchDetailsAsync(device);
            UpdateDeviceInfo(device, details);

            var managed = mDeviceStore.RememberDevice(device);
            managed.IsOnline = true;

            DiscoveredDevices.Remove(device);
        }
        catch (NotPairedException)
        {
            await Shell.Current.GoToAsync($"{nameof(PairingPage)}?ip={device.IpAddress}&port={device.ApiPort}");
        }
        catch (TimeoutException ex)
        {
            mLogger.LogWarning(ex, "Device {Ip} did not respond with details in time", device.IpAddress);
        }
        catch (Exception ex)
        {
            mLogger.LogWarning(ex, "Failed to remember device at {Ip}", device.IpAddress);
        }
    }

    [RelayCommand]
    public async Task Forget(ManagedDevice device)
    {
        await mSignalService.ForgetAsync(device.Device.IpAddress);
        mDeviceStore.ForgetDevice(device.Device);
    }

    public void Dispose()
    {
        mScanner.OnBeaconFound -= OnBeaconFound;
        mSignalService.DeviceStatusChanged -= OnDeviceStatusChanged;

        WeakReferenceMessenger.Default.Unregister<PairingSucceededMessage>(this);
    }
}