using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Android.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IUdpBeaconScanner mScanner;
    private readonly ISignalRService mSignalRService;
    private readonly ILogger<MainViewModel> mLogger;

    [ObservableProperty]
    private partial bool IsScanning { get; set; }

    public ObservableCollection<BeaconDevice> Devices { get; } = new();

    public MainViewModel(IUdpBeaconScanner scanner, ISignalRService signalRService, ILogger<MainViewModel> logger)
    {
        mScanner = scanner;
        mSignalRService = signalRService;
        mLogger = logger;
        
        mScanner.OnBeaconFound += OnBeaconFound;

        mSignalRService.DeviceDetailsReceived += OnDeviceDetailsReceived;
        mSignalRService.ServerRequestedDisconnect += OnServerRequestedDisconnect;
    }

    private void OnBeaconFound(DiscoveredBeacon beacon)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (Devices.Any(d => d.IpAddress == beacon.IpAddress)) return;

            var newDevice = new BeaconDevice
            {
                IpAddress = beacon.IpAddress,
                ApiPort = beacon.Port
            };

            Devices.Add(newDevice);

            // Trigger connection via service
            string hubUrl = $"http://{newDevice.IpAddress}:{newDevice.ApiPort}/beaconHub";
            await mSignalRService.ConnectAsync(newDevice.IpAddress, hubUrl);
        });
    }

    private void OnDeviceDetailsReceived(string ipAddress, BeaconDevice data)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var device = Devices.FirstOrDefault(d => d.IpAddress == ipAddress);
            if (device != null)
            {
                device.MachineName = data.MachineName;
                device.MacAddress = data.MacAddress;
                device.InterfaceType = data.InterfaceType;
                device.InterfaceName = data.InterfaceName;
                mLogger.LogInformation("Updated UI for {Ip}", ipAddress);
            }
        });
    }

    private async void OnServerRequestedDisconnect(string ipAddress)
    {
        mLogger.LogInformation("Disconnecting from {Ip} per server request", ipAddress);
        await mSignalRService.DisconnectAsync(ipAddress);

        /*MainThread.BeginInvokeOnMainThread(() =>
        {
            var device = Devices.FirstOrDefault(d => d.IpAddress == ipAddress);
            if (device != null) Devices.Remove(device);
        });*/
    }

    [RelayCommand]
    public async Task StartScanAsync()
    {
        if (IsScanning) return;

        IsScanning = true;
        Devices.Clear();

        try
        {
            await mScanner.ScanAsync(3000);
        }
        finally
        {
            IsScanning = false;
        }
    }

    public void Dispose()
    {
        // Cleanup all
        mScanner.OnBeaconFound -= OnBeaconFound;
        mSignalRService.DeviceDetailsReceived -= OnDeviceDetailsReceived;
        mSignalRService.ServerRequestedDisconnect -= OnServerRequestedDisconnect;

        mLogger.LogInformation("MainViewModel disposed");
    }
}