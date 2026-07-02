using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Android.Pages;
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
    private readonly ISignalService mSignalService;
    private readonly ILogger<MainViewModel> mLogger;
    private readonly DeviceStore mDeviceStore;

    public ObservableCollection<ManagedDevice> ManagedDevices => mDeviceStore.ManagedDevices;

    public MainViewModel(
        DeviceStore store,
        ISignalService signalService,
        ILogger<MainViewModel> logger)
    {
        mDeviceStore = store;
        mSignalService = signalService;
        mLogger = logger;

        mSignalService.DeviceStatusChanged += OnDeviceStatusChanged;
    }

    private void OnDeviceStatusChanged(string ipAddress, bool isOnline)
    {
        var device = ManagedDevices.FirstOrDefault(d => d.Device.IpAddress == ipAddress);
        if (device != null)
        {
            device.IsOnline = isOnline;
            // CanExecute depends on ManagedDevice.IsOnline, which just
            // changed. Notify the commands so the buttons re-evaluate
            // their enabled state.
            ManageAudioCommand.NotifyCanExecuteChanged();
            ManageDisplayCommand.NotifyCanExecuteChanged();
        }
    }

    private static bool CanManageDevice(ManagedDevice? device) => device is { IsOnline: true };

    [RelayCommand(CanExecute = nameof(CanManageDevice))]
    public async Task ManageAudio(ManagedDevice device)
    {
        await Shell.Current.GoToAsync($"{nameof(AudioControlPage)}?ip={device.Device.IpAddress}");
    }

    [RelayCommand(CanExecute = nameof(CanManageDevice))]
    public async Task ManageDisplay(ManagedDevice device)
    {
        await Shell.Current.GoToAsync($"{nameof(DisplayControlPage)}?ip={device.Device.IpAddress}");
    }

    [RelayCommand]
    public async Task Forget(ManagedDevice device)
    {
        bool confirm = await Shell.Current.CurrentPage.DisplayAlertAsync("Forget Device",
        $"Forget {device.Device.MachineName}? The pairing key will be removed.",
        "Forget", "Cancel");

        if (!confirm) return;

        await mSignalService.ForgetAsync(device.Device.IpAddress);
        mDeviceStore.ForgetDevice(device.Device);
    }

    public void Dispose()
    {
        mSignalService.DeviceStatusChanged -= OnDeviceStatusChanged;
    }
}
