using Android.Provider;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models;
using PcBeaconAgent.Client.Core.Stores;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Uri = Android.Net.Uri;

namespace PcBeaconAgent.Client.Android.ViewModels;

/// <summary>
/// ViewModel for <see cref="Pages.ShareFilePage"/> — the bottom-sheet-
/// styled modal that appears when the user shares a file into the app
/// via the Android Share Sheet. Shows the file name preview and a list
/// of managed devices; tapping a device sends the file and dismisses
/// the modal.
/// </summary>
/// <remarks>
/// The shared file URI is handed off via the static
/// <see cref="PendingFileUri"/> field. The URI is an
/// <c>Android.Net.Uri</c> (typed as <c>object?</c> here to avoid a
/// direct Android dependency in the VM signature — the actual read
/// happens in <see cref="SendToDeviceAsync"/> via
/// <c>Platform.CurrentActivity.ContentResolver</c>).
/// </remarks>
public partial class ShareFileViewModel : ObservableObject, IDisposable
{
    private readonly DeviceStore mDeviceStore;
    private readonly ISignalService mSignalService;
    private readonly ILogger<ShareFileViewModel> mLogger;

    /// <summary>
    /// File URI passed from the Android Share Sheet via a static
    /// hand-off. The URI points to a content:// or file:// resource
    /// that the source app granted read access to (via
    /// FLAG_GRANT_READ_URI_PERMISSION).
    /// </summary>
    public static object? PendingFileUri { get; set; }

    [ObservableProperty]
    public partial string FileName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FileSize { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDevices))]
    [NotifyPropertyChangedFor(nameof(HasNoDevices))]
    public partial bool HasDevices { get; set; }

    public bool HasNoDevices => !HasDevices;

    /// <summary>
    /// Devices the user can send to. Only online devices are shown —
    /// sending to an offline device would fail immediately.
    /// </summary>
    public ObservableCollection<ManagedDevice> Devices { get; } = [];

    [ObservableProperty]
    public partial bool IsSending { get; set; }

    [ObservableProperty]
    public partial string? SendingToDeviceName { get; set; }

    public ShareFileViewModel(DeviceStore deviceStore, ISignalService signalService, ILogger<ShareFileViewModel> logger)
    {
        mDeviceStore = deviceStore;
        mSignalService = signalService;
        mLogger = logger;

        mSignalService.DeviceStatusChanged += OnDeviceStatusChanged;
    }

    /// <summary>
    /// Called by the page on <c>OnAppearing</c>. Consumes the pending
    /// file URI and refreshes the device list.
    /// </summary>
    public void OnPageAppearing()
    {
        if (PendingFileUri != null)
        {
            // Extract the file name from the URI — the Android
            // ContentResolver can query DISPLAY_NAME for content://
            // URIs. For file:// URIs we use the path directly.
            (FileName, FileSize) = ResolveFileInfo(PendingFileUri);
        }

        RefreshDevices();
    }

    private void OnDeviceStatusChanged(string ipAddress, bool isOnline)
    {
        MainThread.BeginInvokeOnMainThread(RefreshDevices);
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
    /// Resolves the display name and size of the shared file URI.
    /// Uses the Android ContentResolver for content:// URIs (the
    /// common case for share-sheet invocations — gallery, file
    /// managers, etc.). Falls back to "(file)" for URIs where the
    /// name cannot be determined.
    /// </summary>
    private (string name, string size) ResolveFileInfo(object uriObj)
    {
#if ANDROID
        try
        {
            var uri = (Uri)uriObj;
            var activity = Platform.CurrentActivity;
            if (activity == null) return ("(file)", "");

            var resolver = activity.ContentResolver;
            if (resolver == null) return ("(file)", "");

            // Query DISPLAY_NAME and SIZE from the content provider.
            // These columns are standard for content:// URIs. For
            // file:// URIs the query may return null — we fall back
            // to the URI's last path segment as the name.
            string? name = null;
            long size = -1;

            using var cursor = resolver.Query(uri, null, null, null, null);
            if (cursor != null && cursor.MoveToFirst())
            {
                int nameIdx = cursor.GetColumnIndex(IOpenableColumns.DisplayName);
                int sizeIdx = cursor.GetColumnIndex(IOpenableColumns.Size);
                if (nameIdx >= 0) name = cursor.GetString(nameIdx);
                if (sizeIdx >= 0 && !cursor.IsNull(sizeIdx)) size = cursor.GetLong(sizeIdx);
            }

            // Fallback: use the URI's last path segment if the cursor
            // did not provide a name (common for file:// URIs).
            name ??= uri.LastPathSegment ?? "(file)";

            string sizeStr = size >= 0 ? FormatFileSize(size) : "";
            return (name, sizeStr);
        }
        catch (Exception ex)
        {
            mLogger.LogWarning(ex, "Failed to resolve file info from share URI");
            return ("(file)", "");
        }
#else
        return ("(file)", "");
#endif
    }

    [RelayCommand]
    public async Task SendToDeviceAsync(ManagedDevice? device)
    {
        if (device == null || IsSending || PendingFileUri == null) return;

        IsSending = true;
        SendingToDeviceName = device.Device.MachineName;

        try
        {
#if ANDROID
            var uri = (Uri)PendingFileUri;
            var activity = Platform.CurrentActivity;
            if (activity == null)
            {
                await Shell.Current.CurrentPage.DisplayAlertAsync("Error", "Cannot access the file.", "OK");
                return;
            }

            // Open the input stream from the ContentResolver. The
            // source app granted read access via
            // FLAG_GRANT_READ_URI_PERMISSION on the share intent, so
            // this works even for content:// URIs from other apps.
            using var inputStream = activity.ContentResolver!.OpenInputStream(uri);
            if (inputStream == null)
            {
                await Shell.Current.CurrentPage.DisplayAlertAsync("Error", "Cannot read the file.", "OK");
                return;
            }

            // Wrap in StreamContent — HttpClient will stream the
            // content to the server without buffering the whole file.
            using var content = new StreamContent(inputStream);

            var response = await device.Transfer.SendFileAsync(content, FileName);
            if (response.Accepted)
            {
                PendingFileUri = null;
                await CloseAsync();
            }
            else
            {
                await Shell.Current.CurrentPage.DisplayAlertAsync("Not sent", response.Message, "OK");
            }
#else
            await Shell.Current.CurrentPage.DisplayAlertAsync("Error", "File sharing is only supported on Android.", "OK");
#endif
        }
        catch (Exception ex)
        {
            mLogger.LogWarning(ex, "Failed to send shared file to {Ip}", device.Device.IpAddress);
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

#if ANDROID
        // Finish() destroys the activity — same AirDrop-like UX as
        // ShareTextViewModel.CloseAsync. See that method for the full
        // rationale (MoveTaskToBack leaves a zombie that breaks the
        // next share).
        var activity = Platform.CurrentActivity;
        activity?.Finish();
#endif
    }

    /// <summary>
    /// Formats a byte count as a human-readable file size string.
    /// Binary prefixes (KB = 1024) to match Windows Explorer.
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

    public void Dispose()
    {
        mSignalService.DeviceStatusChanged -= OnDeviceStatusChanged;
        GC.SuppressFinalize(this);
    }
}
