using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Core.Models;
using PcBeaconAgent.Client.Core.Stores;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Android.ViewModels;

/// <summary>
/// ViewModel for <see cref="Pages.ShareTextPage"/> — the bottom-sheet-
/// styled modal that appears when the user shares text into the app
/// via the Android Share Sheet. Shows the shared text preview and a
/// list of managed devices; tapping a device sends the text and
/// dismisses the modal.
/// </summary>
/// <remarks>
/// This VM is resolved transiently from DI each time the share sheet
/// opens. The shared text itself is handed off via the static
/// <see cref="PendingSharedText"/> field because the Android Intent
/// that starts the share flow cannot directly inject into MAUI DI —
/// the Intent arrives on the native <c>MainActivity</c> side, and the
/// MAUI side picks it up on the next navigation.
/// </remarks>
public partial class ShareTextViewModel : ObservableObject
{
    private readonly DeviceStore mDeviceStore;
    private readonly ILogger<ShareTextViewModel> mLogger;

    /// <summary>
    /// Text passed from the Android Share Sheet via a static hand-off.
    /// <see cref="Pages.ShareTextPage"/> reads and clears this on
    /// <c>OnAppearing</c>.
    /// </summary>
    public static string? PendingSharedText { get; set; }

    [ObservableProperty]
    public partial string SharedText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Preview { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDevices))]
    [NotifyPropertyChangedFor(nameof(HasNoDevices))]
    public partial bool HasDevices { get; set; }

    public bool HasNoDevices => !HasDevices;

    /// <summary>
    /// Devices the user can send to. Only online devices are shown —
    /// sending to an offline device would fail immediately, so hiding
    /// them avoids a dead-end tap.
    /// </summary>
    public ObservableCollection<ManagedDevice> Devices { get; } = [];

    [ObservableProperty]
    public partial bool IsSending { get; set; }

    [ObservableProperty]
    public partial string? SendingToDeviceName { get; set; }

    public ShareTextViewModel(DeviceStore deviceStore, ILogger<ShareTextViewModel> logger)
    {
        mDeviceStore = deviceStore;
        mLogger = logger;
    }

    /// <summary>
    /// Called by the page on <c>OnAppearing</c>. Consumes the pending
    /// shared text and refreshes the device list.
    /// </summary>
    public void OnPageAppearing()
    {
        if (!string.IsNullOrEmpty(PendingSharedText))
        {
            SharedText = PendingSharedText;
            PendingSharedText = null;
            Preview = BuildPreview(SharedText);
        }

        RefreshDevices();
    }

    private void RefreshDevices()
    {
        Devices.Clear();
        foreach (var device in mDeviceStore.ManagedDevices.Where(d => d.IsOnline))
        {
            Devices.Add(device);
        }
        HasDevices = Devices.Count > 0;
    }

    /// <summary>
    /// Builds a short preview of the shared text for the header.
    /// First line, truncated to 80 chars with ellipsis.
    /// </summary>
    private static string BuildPreview(string text)
    {
        int newlineIdx = text.IndexOf('\n');
        string firstLine = newlineIdx >= 0
            ? text.Substring(0, newlineIdx).TrimEnd('\r')
            : text;

        const int MaxPreview = 80;
        if (firstLine.Length <= MaxPreview)
            return firstLine;

        return firstLine.Substring(0, MaxPreview) + "…";
    }

    [RelayCommand]
    public async Task SendToDeviceAsync(ManagedDevice? device)
    {
        if (device == null || IsSending) return;

        IsSending = true;
        SendingToDeviceName = device.Device.MachineName;

        try
        {
            var response = await device.Transfer.SendTextAsync(SharedText);
            if (response.Accepted)
            {
                await CloseAsync();
            }
            else
            {
                await Shell.Current.CurrentPage.DisplayAlertAsync("Not sent", response.Message, "OK");
            }
        }
        catch (Exception ex)
        {
            mLogger.LogWarning(ex, "Failed to send shared text to {Ip}", device.Device.IpAddress);
            await Shell.Current.CurrentPage.DisplayAlertAsync("Error", "Could not send. Check the connection.", "OK");
        }
        finally
        {
            IsSending = false;
            SendingToDeviceName = null;
        }
    }

    [RelayCommand]
    public async Task CloseAsync()
    {
        await Shell.Current.GoToAsync("//MainPage");
    }
}
