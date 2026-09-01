using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Core.Models;
using PcBeaconAgent.Client.Core.Stores;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Android.ViewModels;

/// <summary>
/// ViewModel for <see cref="Pages.ReceivedPage"/>. Binds to
/// <see cref="ReceivedTransferStore.Items"/> and provides Copy /
/// Open commands for text and file transfers respectively.
/// </summary>
public partial class ReceivedViewModel : ObservableObject
{
    private readonly ReceivedTransferStore mStore;
    private readonly ILogger<ReceivedViewModel> mLogger;

    /// <summary>
    /// Received transfers, newest first. Bound directly to the
    /// store's ObservableCollection.
    /// </summary>
    public ObservableCollection<ReceivedTransfer> Items => mStore.Items;

    public bool HasItems => Items.Count > 0;

    public ReceivedViewModel(ReceivedTransferStore store, ILogger<ReceivedViewModel> logger)
    {
        mStore = store;
        mLogger = logger;
        mStore.Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasItems));
    }

    [RelayCommand]
    public async Task CopyTextAsync(ReceivedTransfer? item)
    {
        if (item == null || item.Kind != ReceivedTransferKind.Text) return;

        try
        {
            await Clipboard.Default.SetTextAsync(item.Text);
            await Shell.Current.CurrentPage.DisplayAlertAsync("Copied", "Text copied to clipboard.", "OK");
        }
        catch (Exception ex)
        {
            mLogger.LogWarning(ex, "Failed to copy text to clipboard");
        }
    }

    [RelayCommand]
    public async Task OpenFileAsync(ReceivedTransfer? item)
    {
        if (item == null || item.Kind != ReceivedTransferKind.File) return;
        if (string.IsNullOrEmpty(item.FilePath) || !System.IO.File.Exists(item.FilePath))
        {
            await Shell.Current.CurrentPage.DisplayAlertAsync("File not found",
                "The file may have been moved or deleted.", "OK");
            return;
        }

        try
        {
#if ANDROID
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            if (activity != null)
            {
                string folder = System.IO.Path.GetDirectoryName(item.FilePath) ?? item.FilePath;
                var file = new Java.IO.File(folder);
                var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(
                    activity, activity.PackageName + ".fileprovider", file);

                var viewIntent = new global::Android.Content.Intent(global::Android.Content.Intent.ActionView);
                viewIntent.SetDataAndType(uri, "resource/folder");
                viewIntent.AddFlags(global::Android.Content.ActivityFlags.GrantReadUriPermission);
                viewIntent.AddFlags(global::Android.Content.ActivityFlags.NewTask);
                activity.StartActivity(global::Android.Content.Intent.CreateChooser(viewIntent, "Open folder"));
            }
#endif
        }
        catch (Exception ex)
        {
            mLogger.LogWarning(ex, "Failed to open file {Path}", item.FilePath);
            await Shell.Current.CurrentPage.DisplayAlertAsync("Cannot open",
                "No app available to open this file type.", "OK");
        }
    }

    [RelayCommand]
    public void Clear()
    {
        mStore.Clear();
    }

#if ANDROID
    private static string GetMimeType(string filePath)
    {
        string ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".txt" => "text/plain",
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".mp4" => "video/mp4",
            ".avi" => "video/x-msvideo",
            ".mkv" => "video/x-matroska",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".zip" => "application/zip",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".html" or ".htm" => "text/html",
            _ => "application/octet-stream"
        };
    }
#endif
}
