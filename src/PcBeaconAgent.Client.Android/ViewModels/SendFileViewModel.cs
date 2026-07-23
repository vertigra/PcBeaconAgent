using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
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
/// ViewModel for <see cref="Pages.SendFilePage"/>. Owns the file picker
/// flow, the selected file preview (name + size), and the send flow.
/// Reached from <see cref="Pages.MainPage"/>'s send-file button on a
/// device card — the device IP is passed via query string.
/// </summary>
[QueryProperty(nameof(DeviceIp), "ip")]
public partial class SendFileViewModel : ObservableObject
{
    private readonly DeviceStore mDeviceStore;
    private readonly ILogger<SendFileViewModel> mLogger;

    [ObservableProperty]
    public partial string DeviceIp { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MachineName { get; set; } = string.Empty;

    /// <summary>
    /// Full path to the picked file on the device. Empty if no file is
    /// selected. Used to open a <see cref="StreamContent"/> for upload.
    /// </summary>
    [ObservableProperty]
    public partial string SelectedFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the picked file (basename only, no path).
    /// Shown in the preview area and sent to the server as the file
    /// name.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFile))]
    [NotifyPropertyChangedFor(nameof(CanSend))]
    public partial string SelectedFileName { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable file size (e.g. "1.2 KB"). Shown in the preview.
    /// </summary>
    [ObservableProperty]
    public partial string SelectedFileSize { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSend))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasError { get; set; }

    public bool HasFile => !string.IsNullOrEmpty(SelectedFileName);

    public bool CanSend => !IsBusy && HasFile;

    public SendFileViewModel(DeviceStore deviceStore, ILogger<SendFileViewModel> logger)
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
    public async Task PickFileAsync()
    {
        try
        {
            // FilePicker.PickAsync opens the system file picker. The
            // default file type is images, but we want any file —
            // pass FilePickerFileType with no filter by using the
            // default options (which on Android maps to
            // "GetContent" with type "*/*").
            var result = await FilePicker.PickAsync(new PickOptions
            {
                // No FileTypes filter — accept any file. On Android
                // this maps to ACTION_GET_CONTENT with type "*/*".
                PickerTitle = "Select a file to send"
            });

            if (result == null)
            {
                // User cancelled the picker — no error, just no
                // selection. Keep the previous selection (if any)
                // so the user can retry without re-picking.
                return;
            }

            // The FileResult exposes the file name and a stream.
            // We capture the path (via FullPath) for later upload —
            // reading the stream here would be premature (the user
            // may cancel the send).
            SelectedFilePath = result.FullPath;
            SelectedFileName = result.FileName;

            // Read the file size for display. OpenReadAsync opens a
            // stream we can query for length without loading the whole
            // file into memory.
            using var stream = await result.OpenReadAsync();
            SelectedFileSize = FormatFileSize(stream.Length);

            StatusMessage = string.Empty;
            HasError = false;
        }
        catch (Exception ex)
        {
            mLogger.LogWarning(ex, "Failed to pick file");
            StatusMessage = "Could not pick the file.";
            HasError = true;
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

        if (string.IsNullOrEmpty(SelectedFilePath) || !File.Exists(SelectedFilePath))
        {
            StatusMessage = "Selected file is no longer available.";
            HasError = true;
            return;
        }

        IsBusy = true;
        StatusMessage = "Sending…";
        HasError = false;

        try
        {
            // Open the file as a stream and wrap it in StreamContent.
            // The HttpClient will stream the content to the server
            // without buffering the entire file in memory — important
            // for large files (videos, archives).
            //
            // The using-scope ensures the FileStream is disposed after
            // the upload completes (or fails). StreamContent does NOT
            // own the stream, so we must dispose it ourselves.
            using var fileStream = File.OpenRead(SelectedFilePath);
            using var content = new StreamContent(fileStream);

            var response = await managed.Transfer.SendFileAsync(content, SelectedFileName);
            if (response.Accepted)
            {
                StatusMessage = $"Sent: {response.FileName}";
                HasError = false;
                // Clear the selection so the user can pick another file.
                SelectedFilePath = string.Empty;
                SelectedFileName = string.Empty;
                SelectedFileSize = string.Empty;
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

    /// <summary>
    /// Formats a byte count as a human-readable file size string
    /// (e.g. "1.2 KB", "3.4 MB"). Binary prefixes (KB = 1024) to
    /// match Windows Explorer's convention.
    /// </summary>
    private static string FormatFileSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        int unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{(int)size} {units[unitIndex]}"
            : $"{size:F1} {units[unitIndex]}";
    }
}
