using Android.OS;
using Microsoft.Extensions.Logging;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using PcBeaconAgent.Client.Android.Pages;
using PcBeaconAgent.Client.Android.ViewModels;
using PcBeaconAgent.Client.Core.Constants;
using PcBeaconAgent.Client.Core.Exceptions;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models;
using PcBeaconAgent.Client.Core.Stores;
using PcBeaconAgent.Contracts.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Android;

public partial class App : Application
{
    private readonly ISignalService mSignalService;
    private readonly DeviceStore mDeviceStore;
    private readonly IPreferencesService mPrefs;
    private readonly ILogger<App> mLogger;

    public ObservableCollection<ManagedDevice> ManagedDevices => mDeviceStore.ManagedDevices;

    public App(DeviceStore store, ISignalService signalRService, IPreferencesService prefs, ILogger<App> logger)
    {
        InitializeComponent();
        mSignalService = signalRService;
        mDeviceStore = store;
        mPrefs = prefs;
        mLogger = logger;

        // Set the client machine name so the server can label this
        // connection in the tray UI. On Android this is the device
        // model (e.g. "Pixel 7", "SM-S908B").
#if ANDROID
        mSignalService.ClientMachineName = Build.Model ?? "Android";
#else
        mSignalService.ClientMachineName = Environment.MachineName;
#endif

        // Subscribe to push events from the PC (Phase 2B).
        mSignalService.TextTransferReceived += OnTextTransferReceived;
        mSignalService.FileTransferReceived += OnFileTransferReceived;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    protected override void OnStart()
    {
        _ = Task.Run(ConnectToManagedDevicesAsync);
        // Share-sheet navigation is handled by MainPage.OnAppearing,
        // which is the most reliable trigger point (see comment there).
        base.OnStart();
    }

    protected override void OnSleep()
    {
        // Intentionally do NOT disconnect SignalR connections here.
        // Previously this called DisconnectAllAsync, which dropped
        // every connection when the app went to background. For the
        // share-sheet flow this was catastrophic: each share required
        // a fresh reconnection (1-2 seconds), during which
        // ShareTextPage showed no devices ("offline"). Keeping
        // connections alive in background is fine for a LAN app —
        // SignalR's WithAutomaticReconnect handles transient drops,
        // and OnResume's ConnectToManagedDevicesAsync re-establishes
        // any connection the OS may have killed during long background.
        base.OnSleep();
    }

    protected override void OnResume()
    {
        _ = Task.Run(ConnectToManagedDevicesAsync);
        // Share-sheet navigation is handled by MainPage.OnAppearing.
        base.OnResume();
    }

    private async Task ConnectToManagedDevicesAsync()
    {
        BeaconDevice? firstNotPairedDevice = null;

        foreach (var device in ManagedDevices.Select(x => x.Device).ToList())
        {
            try
            {
                await mSignalService.ConnectToBeaconHubAsync(device);
            }
            catch (NotPairedException ex)
            {
                mLogger.LogWarning(ex, "Device {Ip} is not paired (key missing or invalid)", device.IpAddress);
                firstNotPairedDevice ??= device;
            }
            catch (Exception ex)
            {
                mLogger.LogWarning(ex, "Failed to reconnect to {Ip} on resume", device.IpAddress);
            }
        }

        if (firstNotPairedDevice != null)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                Shell.Current.GoToAsync($"{nameof(PairingPage)}?ip={firstNotPairedDevice.IpAddress}&port={firstNotPairedDevice.ApiPort}"));
        }
    }

    // ── PC → Android push event handlers ──────────────────────────

    /// <summary>
    /// Called when the PC pushes a text transfer via SignalR. Fires on
    /// the SignalR thread pool — marshals to the UI thread for
    /// clipboard access and alerts.
    /// </summary>
    private void OnTextTransferReceived(string sourceIp, string text, string sourceMachine)
    {
        mLogger.LogInformation("Text transfer received from {Ip} ({Machine}): {Length} chars",
            sourceIp, sourceMachine, text.Length);

        bool autoCopy = mPrefs.Get(StorageKeys.AutoCopyReceivedText, true);

        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (autoCopy)
            {
                try
                {
                    await Clipboard.Default.SetTextAsync(text);
                    await ShowAlertAsync("Text received",
                        $"From {sourceMachine} — copied to clipboard.\n\n{text}");
                }
                catch
                {
                    await ShowAlertAsync("Text received",
                        $"From {sourceMachine} — could not copy to clipboard.\n\n{text}");
                }
            }
            else
            {
                await ShowAlertAsync("Text received",
                    $"From {sourceMachine}:\n\n{text}");
            }
        });
    }

    /// <summary>
    /// Called when the PC pushes a file transfer notification via
    /// SignalR. Fires on the SignalR thread pool. Downloads the file
    /// via HTTP (the download URL was included in the push event) and
    /// saves it to the app's data directory.
    /// </summary>
    private void OnFileTransferReceived(string sourceIp, string fileName, long sizeBytes, string downloadUrl, string sourceMachine)
    {
        mLogger.LogInformation("File transfer received from {Ip} ({Machine}): {File} ({Size} bytes)",
            sourceIp, sourceMachine, fileName, sizeBytes);

        // Resolve the API key for the source PC — the download endpoint
        // requires it. The key is stored per-IP in preferences.
        string apiKey = mPrefs.Get(StorageKeys.ApiKeyFor(sourceIp), string.Empty);

        _ = Task.Run(async () =>
        {
            try
            {
                using var http = new HttpClient();
                if (!string.IsNullOrEmpty(apiKey))
                    http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

                // Download the file content. The response is streamed
                // directly to disk — we don't buffer the whole file in
                // memory.
                using var response = await http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                // Save to the app's data directory under a PcBeaconAgent
                // subfolder. On Android this is app-private storage — the
                // user can access it via a file manager at
                // /data/data/com.vertigra.beaconclient/files/PcBeaconAgent/.
                // A future improvement would use the public Downloads
                // folder via the Storage Access Framework.
                string saveFolder = Path.Combine(FileSystem.AppDataDirectory, "PcBeaconAgent");
                Directory.CreateDirectory(saveFolder);

                // Sanitise the file name and handle collisions.
                string safeName = Path.GetFileName(fileName);
                if (string.IsNullOrEmpty(safeName))
                    safeName = $"transfer-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

                string savePath = Path.Combine(saveFolder, safeName);
                if (File.Exists(savePath))
                {
                    string baseName = Path.GetFileNameWithoutExtension(safeName);
                    string ext = Path.GetExtension(safeName);
                    int suffix = 1;
                    while (File.Exists(savePath))
                    {
                        savePath = Path.Combine(saveFolder, $"{baseName} ({suffix}){ext}");
                        suffix++;
                    }
                }

                using var fileStream = File.Create(savePath);
                await response.Content.CopyToAsync(fileStream);

                await MainThread.InvokeOnMainThreadAsync(() =>
                    ShowAlertAsync("File received",
                        $"From {sourceMachine}: {Path.GetFileName(savePath)} ({FormatFileSize(sizeBytes)})"));
            }
            catch (Exception ex)
            {
                mLogger.LogWarning(ex, "Failed to download file from {Url}", downloadUrl);
                await MainThread.InvokeOnMainThreadAsync(() =>
                    ShowAlertAsync("File download failed",
                        $"Could not download {fileName}. Check the connection."));
            }
        });
    }

    /// <summary>
    /// Shows an alert on the current page. No-op if there is no current
    /// page (e.g. the app is in the background — the event is still
    /// logged, and the file is still saved).
    /// </summary>
    private static async Task ShowAlertAsync(string title, string message)
    {
        Page? page = Shell.Current?.CurrentPage;
        if (page != null)
            await page.DisplayAlertAsync(title, message, "OK");
    }

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
        return unitIndex == 0 ? $"{(int)size} {units[unitIndex]}" : $"{size:F1} {units[unitIndex]}";
    }
}