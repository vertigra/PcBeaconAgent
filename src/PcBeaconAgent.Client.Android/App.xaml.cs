using Android.OS;
using Microsoft.Extensions.Logging;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using PcBeaconAgent.Client.Android.Pages;
using PcBeaconAgent.Client.Android.ViewModels;
using PcBeaconAgent.Client.Android.Services;
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
    private readonly ReceivedTransferStore mReceivedStore;
    private readonly ILogger<App> mLogger;

    public ObservableCollection<ManagedDevice> ManagedDevices => mDeviceStore.ManagedDevices;

    public App(DeviceStore store, ISignalService signalRService, IPreferencesService prefs,
               ReceivedTransferStore receivedStore, ILogger<App> logger)
    {
        InitializeComponent();
        mSignalService = signalRService;
        mDeviceStore = store;
        mPrefs = prefs;
        mReceivedStore = receivedStore;
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

        // Request notification permission (Android 13+). The system
        // shows a dialog on first launch. If denied, notifications
        // silently fail — transfers still appear on the Received page.
        AndroidNotificationService.RequestPermission();

        // Share-sheet navigation is handled by MainPage.OnAppearing.
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

        // Store in the received-transfer store so it shows on the
        // Received page.
        var transfer = new ReceivedTransfer
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = ReceivedTransferKind.Text,
            Text = text,
            ReceivedAtUtc = DateTime.UtcNow,
            SourceMachine = sourceMachine
        };
        mReceivedStore.Add(transfer);

        // Show a notification in the tray (works even when app is
        // in background).
        string notifTitle = "Text received";
        string notifBody = autoCopy ? $"From {sourceMachine} — copied to clipboard" : $"From {sourceMachine}";
        AndroidNotificationService.ShowNotification(notifTitle, notifBody);

        if (autoCopy)
        {
            _ = MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try { await Clipboard.Default.SetTextAsync(text); }
                catch { /* clipboard busy — text still in store */ }
            });
        }
    }

    private void OnFileTransferReceived(string sourceIp, string fileName, int sizeBytes, string downloadUrl, string sourceMachine)
    {
        mLogger.LogInformation("File transfer received from {Ip} ({Machine}): {File} ({Size} bytes)",
            sourceIp, sourceMachine, fileName, sizeBytes);

        // Show notification IMMEDIATELY — before the download starts.
        // The user needs to know a file is incoming, even if the
        // download takes time or fails.
        AndroidNotificationService.ShowNotification("File incoming",
            $"From {sourceMachine}: {fileName} ({FormatFileSize(sizeBytes)}) — downloading…");

        string apiKey = mPrefs.Get(StorageKeys.ApiKeyFor(sourceIp), string.Empty);

        _ = Task.Run(async () =>
        {
            string? savedPath = null;
            try
            {
                using var http = new HttpClient();
                if (!string.IsNullOrEmpty(apiKey))
                    http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

                using var response = await http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                string saveFolder = Path.Combine(FileSystem.AppDataDirectory, "PcBeaconAgent");
                Directory.CreateDirectory(saveFolder);

                string safeName = Path.GetFileName(fileName);
                if (string.IsNullOrEmpty(safeName))
                    safeName = $"transfer-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

                savedPath = Path.Combine(saveFolder, safeName);
                if (File.Exists(savedPath))
                {
                    string baseName = Path.GetFileNameWithoutExtension(safeName);
                    string ext = Path.GetExtension(safeName);
                    int suffix = 1;
                    while (File.Exists(savedPath))
                    {
                        savedPath = Path.Combine(saveFolder, $"{baseName} ({suffix}){ext}");
                        suffix++;
                    }
                }

                using var fileStream = File.Create(savedPath);
                await response.Content.CopyToAsync(fileStream);
            }
            catch (Exception ex)
            {
                mLogger.LogWarning(ex, "Failed to download file from {Url}", downloadUrl);
                AndroidNotificationService.ShowNotification("File download failed",
                    $"Could not download {fileName}.");
                return;
            }

            // Store after download so the Open button can find the file.
            var transfer = new ReceivedTransfer
            {
                Id = Guid.NewGuid().ToString("N"),
                Kind = ReceivedTransferKind.File,
                FileName = Path.GetFileName(savedPath),
                FilePath = savedPath ?? string.Empty,
                SizeBytes = sizeBytes,
                ReceivedAtUtc = DateTime.UtcNow,
                SourceMachine = sourceMachine
            };
            mReceivedStore.Add(transfer);

            // Update notification — download complete.
            AndroidNotificationService.ShowNotification("File received",
                $"From {sourceMachine}: {Path.GetFileName(savedPath)} ({FormatFileSize(sizeBytes)}) — tap Received tab to open");
        });
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