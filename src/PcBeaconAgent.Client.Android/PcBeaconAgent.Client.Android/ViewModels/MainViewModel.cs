using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Android.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IUdpDiscoveryService mDiscoveryService;
    private readonly ILogger<MainViewModel> mLogger;
    private IDispatcherTimer? mUiTimer;
    private CancellationTokenSource? mTokenSource;

    [ObservableProperty]
    public partial bool IsScanning { get; set; }

    public ReadOnlyObservableCollection<BeaconDevice> Devices => mDiscoveryService.DiscoveredDevices;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// </summary>
    public MainViewModel(IUdpDiscoveryService discoveryService, ILogger<MainViewModel> logger)
    {
        mDiscoveryService = discoveryService;
        mLogger = logger;
    }

    [RelayCommand]
    public async Task StartScanAsync()
    {
        if (IsScanning) return;

        // Инициализируем таймер лениво, чтобы не упасть при раннем старте Android-приложения
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
            // Запускаем сетевой цикл в бэкграунд-потоке, чтобы не вешать UI
            await Task.Run(async () =>
            {
                await mDiscoveryService.StartScanningAsync(mTokenSource.Token);
            });

            mUiTimer.Start();
        }
        catch (Exception ex)
        {
            // Логируем ошибку, которая теперь железно запишется в лог-систему
            mLogger.LogError(ex, "Failed to start UDP discovery scanning loop.");

            IsScanning = false;
            mUiTimer?.Stop();
            mTokenSource?.Cancel();
        }
    }

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