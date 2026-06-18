using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models;
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

    public MainViewModel(DeviceStore store, IUdpBeaconScannerService scanner, ISignalService signalRService, ILogger<MainViewModel> logger)
    {
        mDeviceStore = store;
        mScanner = scanner;
        mSignalService = signalRService;
        mLogger = logger;

        mScanner.OnBeaconFound += OnBeaconFound;
        mSignalService.DeviceDetailsReceived += OnDeviceDetailsReceived;
        mSignalService.DeviceStatusChanged += OnDeviceStatusChanged;
    }

    private void OnDeviceStatusChanged(string ipAddress, bool isOnline)
    {
        var device = ManagedDevices.FirstOrDefault(d => d.Device.IpAddress == ipAddress);
        
        if (device == null)
            return;
        
        device.IsOnline = isOnline;
    }

    private void OnBeaconFound(DiscoveredBeacon beacon)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (ManagedDevices.Any(d => d.Device.IpAddress == beacon.IpAddress))
                return; 

            if (DiscoveredDevices.Any(d => d.IpAddress == beacon.IpAddress))
                return;

            var newDevice = new BeaconDevice { IpAddress = beacon.IpAddress, ApiPort = beacon.Port };
            DiscoveredDevices.Add(newDevice);

            try
            {
                await mSignalService.ReceiveDeviceDetailsAndCloseAsync(newDevice);
            }
            catch (Exception ex)
            {
                mLogger.LogWarning(ex, "Failed to connect to discovered device at {Ip}", newDevice.IpAddress);
                DiscoveredDevices.Remove(newDevice);
            }
        });
    }

    private void OnDeviceDetailsReceived(string ipAddress, BeaconDevice data)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var discovered = DiscoveredDevices.FirstOrDefault(d => d.IpAddress == ipAddress);
            if (discovered != null)
            {
                UpdateDeviceInfo(discovered, data);
                int idx = DiscoveredDevices.IndexOf(discovered);
                DiscoveredDevices[idx] = discovered;

                mLogger.LogInformation("Updated UI for {Ip}", ipAddress);
            }
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
    public async Task StartScanAsync()
    {
        IsScanning = true;
        DiscoveredDevices.Clear();
        await mScanner.ScanAsync(3000);
        IsScanning = false;
    }

    [RelayCommand]
    public async Task Remember(BeaconDevice device)
    {
        mDeviceStore.RememberDevice(device);
        await mSignalService.ConnectToBeaconHubAsync(device);
        DiscoveredDevices.Remove(device);
    }

    [RelayCommand]
    public async Task Forget(ManagedDevice device) 
    {
        await mSignalService.DisconnectBeaconHubAsync(device.Device);
        mDeviceStore.ForgetDevice(device.Device);
    } 

    public void Dispose()
    {
        mScanner.OnBeaconFound -= OnBeaconFound;
        mSignalService.DeviceDetailsReceived -= OnDeviceDetailsReceived;
        mSignalService.DeviceStatusChanged -= OnDeviceStatusChanged;
    }
}