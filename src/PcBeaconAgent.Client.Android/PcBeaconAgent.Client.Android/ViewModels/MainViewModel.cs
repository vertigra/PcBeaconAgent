using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models;

namespace PcBeaconAgent.Client.Android.ViewModels;

/// <summary>
/// ViewModel managing the primary discovery UI logic, controlling 
/// scan lifecycle triggers and managing active device state updates.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IUdpDiscoveryService mDiscoveryService;
    private readonly ILogger<MainViewModel> mLogger;
    private IDispatcherTimer? mUiTimer;
    private CancellationTokenSource? mTokenSource;

    /// <summary>
    /// Gets or sets a value indicating whether the discovery service is actively scanning for beacon devices.
    /// </summary>
    [ObservableProperty]
    public partial bool IsScanning { get; set; }

    /// <summary>
    /// Gets the read-only collection of discovered beacon agents currently active on the network.
    /// </summary>
    public ReadOnlyObservableCollection<BeaconDevice> Devices => mDiscoveryService.DiscoveredDevices;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// </summary>
    /// <param name="discoveryService">The underlying service handles network-level UDP broadcast probes.</param>
    /// <param name="logger">The logging mechanism capturing runtime UI-bound lifecycle exceptions.</param>
    public MainViewModel(IUdpDiscoveryService discoveryService, ILogger<MainViewModel> logger)
    {
        mDiscoveryService = discoveryService;
        mLogger = logger;
    }

    /// <summary>
    /// Initiates the background UDP discovery process and registers the periodic offline timeout verification routine.
    /// </summary>
    [RelayCommand]
    public async Task StartScanAsync()
    {
        if (IsScanning) return;

        // Initialize the UI thread dispatch timer lazily to ensure safe runtime binding during early Android app boot
        if (mUiTimer == null)
        {
            mUiTimer = Application.Current!.Dispatcher.CreateTimer();
            mUiTimer.Interval = TimeSpan.FromSeconds(3);
            mUiTimer.Tick += (s, e) => mDiscoveryService.CheckTimeouts();
        }

        mTokenSource = new CancellationTokenSource();
        IsScanning = true;

        try
        {
            // Offload the network socket processing loop onto a background worker thread to prevent rendering freezes
            await Task.Run(async () =>
            {
                await mDiscoveryService.StartScanningAsync(mTokenSource.Token);
            });

            mUiTimer.Start();
        }
        catch (Exception ex)
        {
            mLogger.LogError(ex, "Failed to start UDP discovery scanning loop.");

            IsScanning = false;
            mUiTimer?.Stop();
            mTokenSource?.Cancel();
        }
    }

    /// <summary>
    /// Aborts the active network scanning context, disposes background tokens, and holds the interface timeout tracking clock.
    /// </summary>
    [RelayCommand]
    public void StopScan()
    {
        if (!IsScanning) return;

        mTokenSource?.Cancel();
        mTokenSource?.Dispose();
        mTokenSource = null;

        mDiscoveryService.StopScanning();
        mUiTimer?.Stop();
        IsScanning = false;
    }
}