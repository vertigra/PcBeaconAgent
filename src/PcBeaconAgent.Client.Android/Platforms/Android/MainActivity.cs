using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Net.Wifi;
using Android.OS;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Android.ViewModels;

namespace PcBeaconAgent.Client.Android;

/// <summary>
/// Main lifecycle activity for the Android application.
/// Manages system-level configurations including Wi-Fi multicast locking.
/// Also handles the Android Share Sheet intent for text — when another
/// app shares text via "Send to PC", the intent is received here and
/// the text is stashed in <see cref="ShareTextViewModel.PendingSharedText"/>
/// before navigating to <see cref="Pages.ShareTextPage"/>.
/// </summary>
[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    // SingleTop ensures that a share-sheet invocation reuses the
    // existing activity instance (firing OnNewIntent) instead of
    // creating a new one. Without this, the default 'standard'
    // launch mode creates a new MainActivity on every share, which
    // breaks the static hand-off pattern (the new instance stashes
    // text, but App.OnStart does not fire again, so nobody
    // navigates).
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter(
    new[] { Intent.ActionSend },
    Categories = new[] { Intent.CategoryDefault },
    DataMimeType = "text/plain",
    Label = "Send to PC",
    // Priority influences the order in the Android Share Sheet —
    // higher values appear earlier. Officially deprecated for share
    // intents since Android Q, but the system still honours it as a
    // tiebreaker when multiple targets match. 100 is a safe high
    // value that does not conflict with system apps (which typically
    // use 0). Some vendor share sheets (Samsung, Xiaomi) sort by
    // usage frequency regardless, so frequent selection lifts us
    // organically on those ROMs.
    Priority = 100)]
public class MainActivity : MauiAppCompatActivity
{
    private WifiManager.MulticastLock? _multicastLock;

    /// <inheritdoc />
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // 1. Request access to the Android system Wi-Fi service
        if (GetSystemService(WifiService) is WifiManager wifi)
        {
            // 2. Create a multicast lock with a unique reference tag
            _multicastLock = wifi.CreateMulticastLock("pc_beacon_multicast_lock");

            // 3. Acquire the lock to prevent the Android network stack from filtering incoming UDP broadcast/multicast packets
            _multicastLock?.Acquire();
        }

        // Consume the share intent if present. The Intent is set by
        // the system when another app invokes "Share → Send to PC".
        // We stash the text in a static field because the MAUI
        // navigation stack is not ready yet at OnCreate time — the
        // App.OnCreated handler will pick it up and navigate to
        // ShareTextPage once the Shell is initialised.
        ConsumeShareIntent(Intent);
    }

    /// <inheritdoc />
    /// <summary>
    /// Called when a new intent arrives while the activity is already
    /// running (single-top behaviour, requires
    /// <see cref="LaunchMode.SingleTop"/>). This is the path for
    /// share-sheet invocations after the first launch — the activity
    /// is reused, and we receive the new text here.
    /// </summary>
    /// <remarks>
    /// This method stashes the shared text AND navigates to
    /// <see cref="Pages.ShareTextPage"/>. We navigate here directly
    /// (not relying on <c>App.OnResume</c>) because after
    /// <c>MoveTaskToBack</c> the <c>OnResume</c> callback proved
    /// unreliable — it either did not fire or fired after the
    /// <c>PendingSharedText</c> was already consumed. Navigating here
    /// is safe because the Shell is already initialised (the app was
    /// running). <c>App.OnResume</c> still has its own navigation
    /// check as a fallback for the cold-start path (new activity
    /// created, <c>OnNewIntent</c> not called) — no race because
    /// <c>ShareTextPage.OnAppearing</c> clears
    /// <c>PendingSharedText</c> before <c>OnResume</c> can read it.
    /// </remarks>
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        ConsumeShareIntent(intent);

        if (!string.IsNullOrEmpty(ShareTextViewModel.PendingSharedText))
        {
            // Navigate on the UI thread (MAUI Shell requirement).
            // No delay — the Shell is already initialised for a
            // warm-start share. No MainPage-reset — if the user is
            // still on ShareTextPage from a previous invocation,
            // Shell.GoToAsync with the same absolute route is a
            // no-op for the page state but OnAppearing will re-fire
            // and re-consume the freshly stashed text.
            _ = MainThread.InvokeOnMainThreadAsync(() =>
                Shell.Current.GoToAsync($"///{nameof(Pages.ShareTextPage)}"));
        }
    }

    private static void ConsumeShareIntent(Intent? intent)
    {
        if (intent?.Action != Intent.ActionSend) return;
        if (intent.Type != "text/plain") return;

        string? text = intent.GetStringExtra(Intent.ExtraText);
        if (string.IsNullOrWhiteSpace(text)) return;

        // Stash for ShareTextPage to consume on OnAppearing. We do not
        // navigate here because the MAUI Shell may not be ready yet
        // (OnCreate path) or the current page may be mid-transition
        // (OnNewIntent path). App.OnCreated or MainPage.OnAppearing
        // will detect the pending text and navigate.
        ShareTextViewModel.PendingSharedText = text;
    }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        // Release the multicast lock when the activity is destroyed to save device battery power
        if (_multicastLock is { IsHeld: true })
        {
            _multicastLock.Release();
        }

        base.OnDestroy();
    }
}