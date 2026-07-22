using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Core.Stores;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Android.ViewModels;

/// <summary>
/// ViewModel for <see cref="Pages.SendTextPage"/>. Owns the text
/// being composed, the device picker, and the send flow. Designed to
/// support two entry points:
/// <list type="bullet">
///   <item>From <c>MainPage</c> — the user taps the send button on a
///       device card and navigates here with the device IP pre-
///       selected.</item>
///   <item>From the Android Share Sheet — a separate
///       <c>ShareTextActivity</c> receives the shared text via Intent
///       and feeds it into the same logic via a static hand-off
///       (see <see cref="PendingSharedText"/>).</item>
/// </list>
/// </summary>
[QueryProperty(nameof(DeviceIp), "ip")]
[QueryProperty(nameof(InitialText), "text")]
public partial class SendTextViewModel : ObservableObject
{
    private readonly DeviceStore mDeviceStore;
    private readonly ILogger<SendTextViewModel> mLogger;

    /// <summary>
    /// Text passed from the Android Share Sheet via a static hand-off.
    /// The share-target activity cannot resolve MAUI DI services
    /// directly (it runs in a separate Intent context), so it stashes
    /// the shared text here before navigating to <c>SendTextPage</c>.
    /// The page reads and clears this on <c>OnNavigatedTo</c>.
    /// </summary>
    public static string? PendingSharedText { get; set; }

    [ObservableProperty]
    public partial string DeviceIp { get; set; } = string.Empty;

    /// <summary>
    /// Initial text to populate the Editor with. Set via query string
    /// when navigating from the Share Sheet. Once consumed by the
    /// page's OnNavigatedTo, this is cleared.
    /// </summary>
    [ObservableProperty]
    public partial string InitialText { get; set; } = string.Empty;

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

    /// <summary>
    /// Called by the page when it appears. Consumes any pending
    /// shared text from the Android Share Sheet hand-off, then falls
    /// back to the query-string InitialText.
    /// </summary>
    public void OnPageAppearing()
    {
        // Consume the static hand-off first — the share-target
        // activity stashes text here before navigating. Clearing
        // immediately prevents a stale value from reappearing if the
        // user navigates away and back.
        if (!string.IsNullOrEmpty(PendingSharedText))
        {
            Text = PendingSharedText;
            PendingSharedText = null;
        }
        else if (!string.IsNullOrEmpty(InitialText))
        {
            Text = InitialText;
            InitialText = string.Empty;
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
