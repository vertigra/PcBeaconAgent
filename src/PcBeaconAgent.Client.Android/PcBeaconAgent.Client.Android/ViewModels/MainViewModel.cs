using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Java.Util.Logging;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Android.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IUdpBeaconScanner mScanner;
    private readonly ILogger<MainViewModel> mLogger;
    private readonly Dictionary<string, HubConnection> mConnections = [];

    [ObservableProperty]
    public partial bool IsScanning { get; set; }

    public ObservableCollection<BeaconDevice> Devices { get; } = [];

    public MainViewModel(IUdpBeaconScanner scanner, ILogger<MainViewModel> logger)
    {
        mScanner = scanner;
        mLogger = logger;
        mScanner.OnBeaconFound += OnBeaconFound;
    }

    private void OnBeaconFound(DiscoveredBeacon beacon)
    {
        MainThread.BeginInvokeOnMainThread(async () => {
            if (!Devices.Any(d => d.IpAddress == beacon.IpAddress))
            {
                var newDevice = new BeaconDevice
                {
                    IpAddress = beacon.IpAddress,
                    ApiPort = beacon.Port
                };

                Devices.Add(newDevice);

                await ConnectToSignalR(newDevice);
            }
        });
    }

    private async Task ConnectToSignalR(BeaconDevice device)
    {
        mLogger.LogInformation("Connecting to SignalR hub at {Ip}:{Port}", device.IpAddress, device.ApiPort);

        var connection = new HubConnectionBuilder()
            .WithUrl($"http://{device.IpAddress}:{device.ApiPort}/beaconHub")
            .WithAutomaticReconnect()
            .Build();

        mConnections[device.IpAddress] = connection;

        connection.On<BeaconDevice>("ReceiveDeviceDetails", (data) =>
        {
            MainThread.BeginInvokeOnMainThread(() => {
                device.MachineName = data.MachineName;
                device.MacAddress = data.MacAddress;
                device.InterfaceType = data.InterfaceType;
                device.InterfaceName = data.InterfaceName;

                mLogger.LogInformation("Received details for {MachineName}", data.MachineName);
            });
        });

        connection.On("CloseConnection", async () =>
        {
            mLogger.LogInformation("Server requested graceful disconnect for {Ip}", device.IpAddress);
            await StopConnection(device.IpAddress);
        });

        try 
        { 
            await connection.StartAsync(); 
        }
        catch (Exception ex) 
        {
            mLogger.LogError(ex, "Failed to connect to {Ip}", device.IpAddress);
        }
    }

    private async Task StopConnection(string ip)
    {
        if (mConnections.TryGetValue(ip, out var connection))
        {
            await connection.StopAsync();
            await connection.DisposeAsync();
            mConnections.Remove(ip);
        }
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
        mScanner.OnBeaconFound -= OnBeaconFound;
        
        foreach (var conn in mConnections.Values) 
            _ = conn.DisposeAsync();

        mConnections.Clear();
        mLogger.LogInformation("MainViewModel disposed");
    }
}