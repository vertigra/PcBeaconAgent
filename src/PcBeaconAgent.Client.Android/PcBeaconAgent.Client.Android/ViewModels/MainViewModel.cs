using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging; // FIX: добавлен using
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Core.Exceptions;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Messages; // FIX: добавлен using
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
        mSignalService.DeviceDetailsReceived += OnDeviceDetailsReceived;
        mSignalService.DeviceStatusChanged += OnDeviceStatusChanged;

        // FIX (новая подписка): когда PairingViewModel сообщает об успешном
        // паринге для конкретного IP, ищем это устройство среди DiscoveredDevices
        // (оно остаётся там после NotPairedException — см. Remember) и повторяем
        // попытку запомнить его автоматически. Защита IsRegistered — на случай
        // если конструктор почему-то вызвался для уже зарегистрированного
        // экземпляра (двойная регистрация иначе бросает исключение).
        if (!WeakReferenceMessenger.Default.IsRegistered<PairingSucceededMessage>(this))
        {
            WeakReferenceMessenger.Default.Register<PairingSucceededMessage>(this, (recipient, message) =>
            {
                var device = DiscoveredDevices.FirstOrDefault(d => d.IpAddress == message.IpAddress);
                if (device != null)
                {
                    // Remember() сам перехватывает все свои исключения и никогда
                    // не выбрасывает их наружу — fire-and-forget здесь безопасен.
                    _ = Remember(device);
                }
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
            var details = await mSignalService.ReceiveDeviceDetailsAndCloseAsync(device);
            UpdateDeviceInfo(device, details);

            // Порядок Connect → Remember сохранён как в вашем варианте: устройство
            // помечается как "запомненное" только после того, как постоянное
            // соединение реально установлено. Альтернатива (Remember → Connect)
            // более отказоустойчива к разовым сетевым сбоям именно в этот момент
            // (устройство всё равно попадёт в ManagedDevices и переподключится
            // позже через App.ConnectToManagedDevicesAsync) — выбор зависит от того,
            // что для вас важнее: строгая гарантия живого соединения на момент
            // Remember, или отказоустойчивость к разовым сбоям. Сейчас оставлен
            // ваш вариант.
            await mSignalService.ConnectToBeaconHubAsync(device);
            mDeviceStore.RememberDevice(device);

            DiscoveredDevices.Remove(device);
        }
        catch (NotPairedException)
        {
            await Shell.Current.GoToAsync(
                $"{nameof(PairingPage)}?ip={device.IpAddress}&port={device.ApiPort}");
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
        mSignalService.DeviceDetailsReceived -= OnDeviceDetailsReceived;
        mSignalService.DeviceStatusChanged -= OnDeviceStatusChanged;

        // FIX: обязательная отписка от мессенджера — иначе при повторном создании
        // экземпляра (например, в будущем если изменится lifetime в DI) попытка
        // зарегистрироваться снова упадёт с исключением "already registered".
        WeakReferenceMessenger.Default.Unregister<PairingSucceededMessage>(this);
    }
}