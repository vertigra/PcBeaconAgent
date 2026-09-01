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

        // Consume the share intent if present, then replace it with a
        // clean empty Intent. Without this, the share Intent "sticks"
        // to the activity — after Finish() and activity recreation,
        // OnCreate would re-receive the same stale share Intent,
        // re-stash the old text, and confuse the navigation flow
        // (PendingSharedText from a previous share would be overwritten
        // or race with a new share).
        ConsumeShareIntent(Intent);
        Intent = new Intent();
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
    /// This method ONLY stashes the shared text. Navigation is handled
    /// by <c>MainPage.OnAppearing</c>, which checks
    /// <c>ShareTextViewModel.PendingSharedText</c> and navigates to
    /// <c>ShareTextPage</c> if set. <c>MainPage.OnAppearing</c> is the
    /// most reliable trigger point — it always fires when a new AppShell
    /// is created, unlike <c>App.OnStart</c>/<c>OnResume</c> which
    /// proved unreliable when the process was alive but the activity
    /// was recreated.
    /// </remarks>
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);

        // Check if this is a notification tap with a file path extra.
        // If so, open the file with the system default app.
        string? filePath = intent?.GetStringExtra(AndroidNotificationService.ExtraFilePath);
        if (!string.IsNullOrEmpty(filePath))
        {
            // Clear the extra so it doesn't re-trigger on next lifecycle event.
            intent?.RemoveExtra(AndroidNotificationService.ExtraFilePath);
            Intent = new Intent();

            // Open the file on the UI thread via native Android Intent.
            var ctx = Platform.CurrentActivity ?? Application.Context;
            _ = MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    if (ctx == null || !System.IO.File.Exists(filePath)) return;

                    var file = new Java.IO.File(filePath);
                    var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(
                        ctx, ctx.PackageName + ".fileprovider", file);

                    var viewIntent = new Intent(Intent.ActionView);
                    viewIntent.SetDataAndType(uri, GetMimeType(filePath));
                    viewIntent.AddFlags(ActivityFlags.GrantReadUriPermission);
                    viewIntent.AddFlags(ActivityFlags.NewTask);

                    ctx.StartActivity(viewIntent);
                }
                catch { /* best effort */ }
            });
            return;
        }

        ConsumeShareIntent(intent);
        // Replace the activity's Intent with a clean one so the share
        // Intent does not "stick" and get re-delivered on the next
        // lifecycle event. See OnCreate for rationale.
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

    /// <summary>
    /// Returns a MIME type for the file based on its extension.
    /// Android requires a MIME type on ACTION_VIEW intents — without
    /// it, no app will match. Returns application/octet-stream as
    /// fallback for unknown extensions.
    /// </summary>
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
}