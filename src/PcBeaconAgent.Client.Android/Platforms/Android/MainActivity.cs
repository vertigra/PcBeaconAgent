using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Net;
using Android.Net.Wifi;
using Android.OS;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using PcBeaconAgent.Client.Android.Services;
using PcBeaconAgent.Client.Android.ViewModels;

namespace PcBeaconAgent.Client.Android.Platforms.Android;

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
    [Intent.ActionSend],
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
// Second intent filter for file sharing — accepts any MIME type.
// The system shows "Send to PC" once for text/plain (above) and once
// for */* (here), but Android deduplicates by component name so the
// user sees a single "Send to PC" entry that accepts both text and
// files. ConsumeShareIntent distinguishes the two by checking for
// ExtraText (text) vs ExtraStream (file).
[IntentFilter(
    [Intent.ActionSend],
    Categories = new[] { Intent.CategoryDefault },
    DataMimeType = "*/*",
    Label = "Send to PC",
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

        // Check for notification extras FIRST (cold start case).
        if (!AndroidNotificationService.HandleNotificationIntent(Intent))
        {
            ConsumeShareIntent(Intent);
        }
        Intent = new Intent();
    }

    /// <inheritdoc />
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);

        if (!AndroidNotificationService.HandleNotificationIntent(intent))
        {
            ConsumeShareIntent(intent);
        }
        Intent = new Intent();
    }

    private static void ConsumeShareIntent(Intent? intent)
    {
        if (intent?.Action != Intent.ActionSend) return;

        // Distinguish text vs file share by checking which extra is
        // present. Text shares carry ExtraText; file shares carry
        // ExtraStream (a content:// or file:// URI). Some apps send
        // both (e.g. a URL as text + a screenshot as stream) — we
        // prioritise text in that case, since text is cheaper to send
        // and the user typically wants the URL, not the screenshot.
        string? text = intent.GetStringExtra(Intent.ExtraText);
        if (!string.IsNullOrWhiteSpace(text))
        {
            ShareTextViewModel.PendingSharedText = text;
            return;
        }

        // File share — extract the stream URI. The URI carries a
        // read permission grant (FLAG_GRANT_READ_URI_PERMISSION) from
        // the source app, so we can open it via ContentResolver in
        // ShareFileViewModel.SendToDeviceAsync.
        //
        // Use the typed GetParcelableExtra(string, Class) overload on
        // Android 13+ (Tiramisu). The parameterless overload is marked
        // obsolete (CA1422). Version-gate for older API levels.
        Uri? streamUri = (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            ? intent.GetParcelableExtra(Intent.ExtraStream, Java.Lang.Class.FromType(typeof(Uri))) as Uri
            : intent.GetParcelableExtra(Intent.ExtraStream) as Uri;
        if (streamUri != null)
        {
            ShareFileViewModel.PendingFileUri = streamUri;
            return;
        }

        // Neither text nor stream — the share intent had no usable
        // payload. Ignore it (the user sees MainPage, no error).
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