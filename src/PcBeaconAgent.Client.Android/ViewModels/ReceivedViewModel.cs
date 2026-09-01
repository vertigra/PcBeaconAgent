using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using PcBeaconAgent.Client.Core.Models;
using PcBeaconAgent.Client.Core.Stores;
using System;
using System.Collections.ObjectModel;
using System.Linq;
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
            // Use MAUI's Launcher.OpenAsync to open the file with the
            // system's default app. On Android, this uses
            // ACTION_VIEW with a FileProvider URI.
            await Launcher.Default.OpenAsync(new Microsoft.Maui.Essentials.FileResult(item.FilePath));
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
}
