using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel; // FIX: добавлено для MainThread
using Microsoft.Maui.Controls;
using Microsoft.Extensions.Logging;
using PcBeaconAgent.Client.Core.Exceptions;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models;
using PcBeaconAgent.Client.Core.Stores;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace PcBeaconAgent.Client.Android;

public partial class App : Application
{
    private readonly ISignalService mSignalService;
    private readonly DeviceStore mDeviceStore;
    private readonly ILogger<App> mLogger;

    public ObservableCollection<ManagedDevice> ManagedDevices => mDeviceStore.ManagedDevices;

    public App(DeviceStore store, ISignalService signalRService, ILogger<App> logger)
    {
        InitializeComponent();
        mSignalService = signalRService;
        mDeviceStore = store;
        mLogger = logger;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    protected override void OnStart()
    {
        _ = Task.Run(ConnectToManagedDevicesAsync);
        base.OnStart();
    }

    protected override void OnSleep()
    {
        _ = Task.Run(DisconnectAllAsync);
        base.OnSleep();
    }

    protected override void OnResume()
    {
        _ = Task.Run(ConnectToManagedDevicesAsync);
        base.OnResume();
    }

    private async Task ConnectToManagedDevicesAsync()
    {
        // FIX: раньше NotPairedException для уже запомненного устройства попадал в
        // общий catch (Exception) ниже — просто логировался как warning, и устройство
        // оставалось "offline" навсегда без какого-либо пути восстановления из UI
        // (кроме Forget → повторный поиск в сети → Remember заново).
        // Типичная причина: ключ этого устройства был перезаписан при паринге с
        // другим сервером (до фикса per-IP хранения) либо ключ на сервере сменился
        // (например, удалён server.key).
        // Теперь для первого такого устройства открывается экран паринга.
        BeaconDevice? firstNotPairedDevice = null;

        foreach (var device in ManagedDevices.Select(x => x.Device).ToList())
        {
            try
            {
                await mSignalService.ConnectToBeaconHubAsync(device);
            }
            catch (NotPairedException ex)
            {
                mLogger.LogWarning(ex, "Device {Ip} is not paired (key missing or invalid)", device.IpAddress);
                firstNotPairedDevice ??= device;
            }
            catch (Exception ex)
            {
                mLogger.LogWarning(ex, "Failed to reconnect to {Ip} on resume", device.IpAddress);
            }
        }

        if (firstNotPairedDevice != null)
        {
            // FIX: навигация через Shell должна выполняться на UI-потоке, а этот
            // метод запускается в фоне через Task.Run из OnStart/OnResume.
            // Если несколько устройств одновременно без ключа — открываем паринг
            // только для первого; остальные останутся "offline" до следующего
            // resume, когда пользователь сможет повторить процесс по очереди.
            await MainThread.InvokeOnMainThreadAsync(() =>
                Shell.Current.GoToAsync(
                    $"{nameof(PairingPage)}?ip={firstNotPairedDevice.IpAddress}&port={firstNotPairedDevice.ApiPort}"));
        }
    }

    private async Task DisconnectAllAsync()
    {
        foreach (var device in ManagedDevices.Select(x => x.Device).ToList())
        {
            try
            {
                await mSignalService.DisconnectBeaconHubAsync(device);
            }
            catch (Exception ex)
            {
                mLogger.LogWarning(ex, "Failed to disconnect from {Ip} on sleep", device.IpAddress);
            }
        }
    }
}