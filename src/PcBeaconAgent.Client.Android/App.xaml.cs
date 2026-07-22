using Microsoft.Extensions.Logging;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Android.Pages;
using PcBeaconAgent.Client.Android.ViewModels;
using PcBeaconAgent.Client.Core.Exceptions;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models;
using PcBeaconAgent.Client.Core.Stores;
using PcBeaconAgent.Contracts.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Android;

public partial class App : Application
{
    private readonly ISignalService mSignalService;
    private readonly DeviceStore mDeviceStore;
    private readonly ILogger<App> mLogger;

    public ObservableCollection<ManagedDevice> ManagedDevices => mDeviceStore.ManagedDevices;

    public App(DeviceStore store, ISignalService signalRService, ILogger<App> logger)
    {
        InitializeComponent();
        mSignalService = signalRService;
        mDeviceStore = store;
        mLogger = logger;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    protected override void OnStart()
    {
        _ = Task.Run(ConnectToManagedDevicesAsync);

        // Cold-start share-sheet path: MainActivity.OnCreate already
        // stashed the shared text. Try to navigate to ShareTextPage —
        // Shell may not be fully initialised yet at OnStart time, so
        // use the retry helper which waits and re-attempts.
        NavigateToSharePageIfPending();

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

        // Warm-start share-sheet path: MainActivity.OnNewIntent already
        // stashed the shared text. Try to navigate — Shell should be
        // ready (app was running), but use the retry helper anyway in
        // case the previous navigation is still settling.
        NavigateToSharePageIfPending();

        base.OnResume();
    }

    /// <summary>
    /// Navigates to <see cref="ShareTextPage"/> if
    /// <see cref="ShareTextViewModel.PendingSharedText"/> is set, with
    /// retry logic to handle the case where the MAUI Shell is not yet
    /// ready to navigate (common during cold start when OnStart fires
    /// before the Shell has finished initialising its navigation
    /// stack).
    /// </summary>
    /// <remarks>
    /// <b>Why retry:</b> <c>Shell.Current.GoToAsync</c> silently fails
    /// (no exception, no navigation) when called before the Shell is
    /// fully initialised, or when another navigation is in flight.
    /// Without retry, the share text remains in
    /// <c>PendingSharedText</c> indefinitely — the user sees MainPage
    /// instead of the bottom sheet, and the share only opens later if
    /// they manually reopen the app (which re-triggers OnAppearing on
    /// MainPage, but that does not navigate either).
    /// <para>
    /// The retry loop runs on the UI thread (Shell navigation requires
    /// it), with a 100ms delay between attempts, up to 10 attempts
    /// (1 second total). Once the text is consumed by
    /// <see cref="ShareTextPage.OnAppearing"/>,
    /// <c>PendingSharedText</c> becomes null and the loop exits early.
    /// </para>
    /// </remarks>
    private static async void NavigateToSharePageIfPending()
    {
        const int maxAttempts = 10;
        const int delayMs = 100;

        for (int i = 0; i < maxAttempts; i++)
        {
            // Re-check on each iteration — ShareTextPage.OnAppearing
            // clears PendingSharedText, so if a previous attempt
            // succeeded we should stop retrying.
            if (string.IsNullOrEmpty(ShareTextViewModel.PendingSharedText))
                return;

            // Shell.Current may be null very early in startup.
            // GoToAsync throws if Shell is not ready. Wrap in
            // try/catch and retry — the exception is transient.
            try
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                    Shell.Current?.GoToAsync($"///{nameof(ShareTextPage)}"));
            }
            catch
            {
                // Shell not ready, or navigation in flight. Retry.
            }

            // Give the Shell time to settle and OnAppearing time to
            // fire (which clears PendingSharedText on success).
            await Task.Delay(delayMs);
        }
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
}