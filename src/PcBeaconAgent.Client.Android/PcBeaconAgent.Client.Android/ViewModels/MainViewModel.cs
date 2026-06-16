using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private readonly ISignalService mSignalRService;
    private readonly DeviceStore mDeviceStore;

    [ObservableProperty] public partial bool IsScanning { get; set; }

    public ObservableCollection<BeaconDevice> DiscoveredDevices { get; } = [];
    public ObservableCollection<ManagedDevice> ManagedDevices => mDeviceStore.ManagedDevices;

    public MainViewModel(DeviceStore store, IUdpBeaconScannerService scanner, ISignalService signalRService)
    {
        mDeviceStore = store;
        mScanner = scanner;
        mSignalRService = signalRService;

        mScanner.OnBeaconFound += OnBeaconFound;
        mSignalRService.DeviceDetailsReceived += OnDeviceDetailsReceived;
        mSignalRService.ServerRequestedDisconnect += OnServerRequestedDisconnect;
    }

    private async void OnServerRequestedDisconnect(string ipAddress)
    {
        await mSignalRService.DisconnectAsync(ipAddress);
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

            string hubUrl = $"http://{newDevice.IpAddress}:{newDevice.ApiPort}/beaconHub";
            await mSignalRService.ConnectAsync(newDevice.IpAddress, hubUrl);
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
            }

            var managed = ManagedDevices.FirstOrDefault(d => d.Device.IpAddress == ipAddress);
            if (managed != null)
            {
                UpdateDeviceInfo(managed.Device, data);
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
    public void Remember(BeaconDevice device)
    {
        mDeviceStore.RememberDevice(device);
        DiscoveredDevices.Remove(device);
    }

    [RelayCommand]
    public void Forget(ManagedDevice device) => mDeviceStore.ForgetDevice(device.Device);

    public void Dispose()
    {
        mScanner.OnBeaconFound -= OnBeaconFound;
        mSignalRService.DeviceDetailsReceived -= OnDeviceDetailsReceived;
        mSignalRService.ServerRequestedDisconnect -= OnServerRequestedDisconnect;
    }
}