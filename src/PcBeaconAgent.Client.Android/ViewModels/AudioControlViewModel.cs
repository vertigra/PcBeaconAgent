using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Stores;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Android.ViewModels;

[QueryProperty(nameof(DeviceIp), "ip")]
public partial class AudioControlViewModel : ObservableObject
{
    private readonly DeviceStore mDeviceStore;
    private readonly ILogger<AudioControlViewModel> mLogger;
    private IAudioServiceClient? mAudio;

    [ObservableProperty]
    public partial string DeviceIp { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MachineName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string ErrorMessage { get; set; } = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public ObservableCollection<AudioDeviceItem> Devices { get; } = [];

    public AudioControlViewModel(DeviceStore deviceStore, ILogger<AudioControlViewModel> logger)
    {
        mDeviceStore = deviceStore;
        mLogger = logger;
    }

    public async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(DeviceIp)) 
            return;

        var managed = mDeviceStore.ManagedDevices.FirstOrDefault(m => m.Device.IpAddress == DeviceIp);
        if (managed == null)
        {
            ErrorMessage = "Device is no longer in the managed list.";
            return;
        }

        MachineName = managed.Device.MachineName;
        mAudio = managed.Audio;

        await RefreshAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (mAudio == null) return;

        ErrorMessage = string.Empty;
        IsBusy = true;

        try
        {
            var devices = await mAudio.GetDevicesAsync();
            var defaultId = await mAudio.GetDefaultDeviceIdAsync();

            Devices.Clear();
            foreach (var d in devices)
                Devices.Add(new AudioDeviceItem(d.Id, d.FullName) { IsDefault = d.Id == defaultId });
        }
        catch (Exception ex)
        {
            mLogger.LogWarning(ex, "Failed to load audio devices for {Ip}", DeviceIp);
            ErrorMessage = "Could not load audio devices. Check the connection.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SetDefault(AudioDeviceItem device)
    {
        if (mAudio == null || device.IsDefault) return;

        ErrorMessage = string.Empty;
        IsBusy = true;

        try
        {
            await mAudio.SetDefaultAsync(device.Id);

            // Обновляем флаги локально вместо полного Refresh — дешевле и
            // не вызывает лишнего мигания списка.
            foreach (var d in Devices)
                d.IsDefault = d.Id == device.Id;
        }
        catch (Exception ex)
        {
            mLogger.LogWarning(ex, "Failed to set default audio device {Id} for {Ip}", device.Id, DeviceIp);
            ErrorMessage = "Could not change the default device.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

// FIX (новый тип): UI-обёртка с состоянием "это сейчас дефолтное устройство?" —
// сам AudioDeviceInfo (Client.Core) — простой record без MVVM-зависимостей,
// то же разделение, что и BeaconDevice/ManagedDevice.
public partial class AudioDeviceItem(string id, string fullName) : ObservableObject
{
    public string Id { get; } = id;
    public string FullName { get; } = fullName;

    [ObservableProperty]
    public partial bool IsDefault { get; set; }
}