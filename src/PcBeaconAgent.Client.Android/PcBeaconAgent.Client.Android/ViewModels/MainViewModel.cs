using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    [ObservableProperty]
    private bool _isScanning;

    public ObservableCollection<DiscoveredBeacon> Devices { get; } = [];

    public MainViewModel(IUdpBeaconScanner scanner)
    {
        mScanner = scanner;
        mScanner.OnBeaconFound += OnBeaconFound;
    }

    private void OnBeaconFound(DiscoveredBeacon beacon)
    {
        MainThread.BeginInvokeOnMainThread(() => {
            if (!Devices.Any(d => d.IpAddress == beacon.IpAddress))
            {
                Devices.Add(beacon);
            }
        });
    }

    [RelayCommand]
    public async Task StartScanAsync()
    {
        if (IsScanning) return;

        IsScanning = true;
        Devices.Clear();

        _ = await mScanner.ScanAsync(3000);

        IsScanning = false;
    }

    public void Dispose()
    {
        mScanner.OnBeaconFound -= OnBeaconFound;
    }
}