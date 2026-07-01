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
public partial class DisplayControlViewModel : ObservableObject
{
    private readonly DeviceStore mDeviceStore;
    private readonly ILogger<DisplayControlViewModel> mLogger;
    private IDisplayServiceClient? mDisplay;

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

    public ObservableCollection<DisplayDeviceItem> Displays { get; } = [];

    public DisplayControlViewModel(DeviceStore deviceStore, ILogger<DisplayControlViewModel> logger)
    {
        mDeviceStore = deviceStore;
        mLogger = logger;
    }

    public async Task LoadAsync()
    {
        var managed = mDeviceStore.ManagedDevices.FirstOrDefault(m => m.Device.IpAddress == DeviceIp);
        if (managed == null)
        {
            ErrorMessage = "Device is no longer in the managed list.";
            return;
        }

        MachineName = managed.Device.MachineName;
        mDisplay = managed.Display;

        await RefreshAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (mDisplay == null) return;

        ErrorMessage = string.Empty;
        IsBusy = true;

        try
        {
            var displays = await mDisplay.GetDisplaysAsync();

            Displays.Clear();
            foreach (var d in displays)
                Displays.Add(new DisplayDeviceItem(d.Id, d.FriendlyName) { IsActive = d.IsActive });

            // Mark the last remaining active display so the UI can block
            // the Disable button. Windows refuses to disable the final
            // active display, so we prevent the attempt client-side.
            var activeItems = Displays.Where(d => d.IsActive).ToList();
            if (activeItems.Count == 1)
                activeItems[0].IsLastActive = true;
        }
        catch (Exception ex)
        {
            mLogger.LogWarning(ex, "Failed to load displays for {Ip}", DeviceIp);
            ErrorMessage = "Could not load displays. Check the connection.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task Disable(DisplayDeviceItem item)
    {
        if (mDisplay == null || !item.IsActive) return;

        ErrorMessage = string.Empty;
        IsBusy = true;

        try
        {
            await mDisplay.DisableAsync(item.Id);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            mLogger.LogWarning(ex, "Failed to disable display {Id} for {Ip}", item.Id, DeviceIp);
            ErrorMessage = string.IsNullOrEmpty(ex.Message) ? "Could not disable the display." : ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task RestoreAll()
    {
        if (mDisplay == null) return;

        ErrorMessage = string.Empty;
        IsBusy = true;

        try
        {
            await mDisplay.RestoreAllAsync();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            mLogger.LogWarning(ex, "Failed to restore displays for {Ip}", DeviceIp);
            ErrorMessage = "Could not restore displays.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public partial class DisplayDeviceItem(string id, string friendlyName) : ObservableObject
{
    public string Id { get; } = id;
    public string FriendlyName { get; } = friendlyName;

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    /// <summary>
    /// True when this is the only active display remaining. The UI uses
    /// this to disable the Disable button — Windows rejects disabling the
    /// last active display, and the server now returns a clear error for
    /// it, but blocking the button up front avoids a round-trip and a
    /// confusing error toast.
    /// </summary>
    [ObservableProperty]
    public partial bool IsLastActive { get; set; }
}