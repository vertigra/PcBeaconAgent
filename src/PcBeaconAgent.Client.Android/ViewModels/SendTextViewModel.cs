using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using PcBeaconAgent.Client.Core.Stores;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Android.ViewModels;

/// <summary>
/// ViewModel for <see cref="Pages.SendTextPage"/>. Owns the text
/// being composed, the file picker for the inline file-send button,
/// and the send flow for both payload types.
/// </summary>
/// <remarks>
/// Reached from <see cref="Pages.MainPage"/>'s send button on a device
/// card — the device IP is passed via query string. The page handles
/// both text (Editor + Paste + Send) and files (📎 File button opens
/// the system file picker and sends inline). No separate file page.
/// </remarks>
[QueryProperty(nameof(DeviceIp), "ip")]
public partial class SendTextViewModel : ObservableObject
{
    private readonly DeviceStore mDeviceStore;
    private readonly ILogger<SendTextViewModel> mLogger;

    [ObservableProperty]
    public partial string DeviceIp { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    [NotifyPropertyChangedFor(nameof(CanSend))]
    public partial string Text { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSend))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string MachineName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasError { get; set; }

    public bool CanSend => !IsBusy && !string.IsNullOrWhiteSpace(Text);

    public SendTextViewModel(DeviceStore deviceStore, ILogger<SendTextViewModel> logger)
    {
        mDeviceStore = deviceStore;
        mLogger = logger;
    }

    partial void OnDeviceIpChanged(string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        var managed = mDeviceStore.ManagedDevices.FirstOrDefault(m => m.Device.IpAddress == value);
        if (managed != null)
        {
            MachineName = managed.Device.MachineName;
        }
    }

    [RelayCommand]
    public async Task PasteFromClipboardAsync()
    {
        try
        {
            // Clipboard.Default.GetTextAsync returns null if the
            // clipboard is empty or contains non-text content.
            string? clipText = await Clipboard.Default.GetTextAsync();
            if (string.IsNullOrEmpty(clipText))
            {
                StatusMessage = "Clipboard is empty.";
                HasError = false;
                return;
            }
            // Append rather than overwrite so the user can build a
            // composite payload by pasting multiple snippets.
            Text = string.IsNullOrEmpty(Text) ? clipText : Text + Environment.NewLine + clipText;
            StatusMessage = string.Empty;
            HasError = false;
        }
        catch (Exception ex)
        {
            mLogger.LogWarning(ex, "Failed to read clipboard");
            StatusMessage = "Could not read clipboard.";
            HasError = true;
        }
    }

    /// <summary>
    /// Opens the system file picker, then sends the picked file to the
    /// device. The file is streamed (not buffered) so memory usage
    /// stays bounded regardless of file size. Same send flow as
    /// <see cref="SendAsync"/> but for files — no separate page needed.
    /// </summary>
    [RelayCommand]
    public async Task PickAndSendFileAsync()
    {
        var managed = mDeviceStore.ManagedDevices.FirstOrDefault(m => m.Device.IpAddress == DeviceIp);
        if (managed == null)
        {
            StatusMessage = "Device is no longer in the managed list.";
            HasError = true;
            return;
        }

        if (!managed.IsOnline)
        {
            StatusMessage = "Device is offline. Try again when connected.";
            HasError = true;
            return;
        }

        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select a file to send"
            });

            if (result == null) return; // user cancelled

            IsBusy = true;
            StatusMessage = $"Sending {result.FileName}…";
            HasError = false;

            using var fileStream = File.OpenRead(result.FullPath);
            using var content = new StreamContent(fileStream);

            var response = await managed.Transfer.SendFileAsync(content, result.FileName);
            if (response.Accepted)
            {
                StatusMessage = $"Sent: {response.FileName}";
                HasError = false;
                await Task.Delay(800);
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                StatusMessage = response.Message;
                HasError = true;
            }
        }
        catch (Exception ex)
        {
            mLogger.LogWarning(ex, "Failed to send file to {Ip}", DeviceIp);
            StatusMessage = "Could not send. Check the connection.";
            HasError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SendAsync()
    {
        if (!CanSend) return;

        var managed = mDeviceStore.ManagedDevices.FirstOrDefault(m => m.Device.IpAddress == DeviceIp);
        if (managed == null)
        {
            StatusMessage = "Device is no longer in the managed list.";
            HasError = true;
            return;
        }

        if (!managed.IsOnline)
        {
            StatusMessage = "Device is offline. Try again when connected.";
            HasError = true;
            return;
        }

        IsBusy = true;
        StatusMessage = "Sending…";
        HasError = false;

        try
        {
            var response = await managed.Transfer.SendTextAsync(Text);
            if (response.Accepted)
            {
                StatusMessage = "Sent.";
                HasError = false;
                Text = string.Empty;
                // Brief delay so the user sees "Sent." before navigating
                // back. Without this the success message would flash
                // too quickly to read.
                await Task.Delay(800);
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                StatusMessage = response.Message;
                HasError = true;
            }
        }
        catch (Exception ex)
        {
            mLogger.LogWarning(ex, "Failed to send text to {Ip}", DeviceIp);
            StatusMessage = "Could not send. Check the connection.";
            HasError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
