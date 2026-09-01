using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using Microsoft.Maui.ApplicationModel;
using PcBeaconAgent.Client.Android.Platforms.Android;
using System;
using System.Diagnostics;
using Debug = System.Diagnostics.Debug;

namespace PcBeaconAgent.Client.Android.Services;

/// <summary>
/// Platform-specific notification helper for showing transfer
/// notifications in the Android notification tray.
/// </summary>
public static class AndroidNotificationService
{
    private const string ChannelId = "transfers";
    private const int NotificationId = 1001;
    private static bool sChannelCreated;

    public const string ExtraFilePath = "extra_file_path";

    /// <summary>
    /// Checks whether the app has permission to post notifications
    /// (Android 13+). On older versions, always returns true.
    /// </summary>
    public static bool HasNotificationPermission()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu)
            return true;

        var activity = Platform.CurrentActivity;
        if (activity == null)
            return false;

        return activity.CheckSelfPermission(
            global::Android.Manifest.Permission.PostNotifications) == 0;
    }

    /// <summary>
    /// Shows a notification. Tap opens the app.
    /// </summary>
    public static void ShowNotification(string title, string message, string? filePath = null)
    {
        var context = Platform.CurrentActivity ?? Application.Context;
        if (context == null)
        {
            Debug.WriteLine("[AndroidNotification] Context is null");
            return;
        }

        if (!HasNotificationPermission())
        {
            Debug.WriteLine("[AndroidNotification] Permission not granted");
            return;
        }

        EnsureChannel(context);

        try
        {
            var intent = new Intent(context, typeof(MainActivity));
            intent.AddFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);

            // If a file path is provided, pass it as an extra so
            // MainActivity.OnNewIntent can open it when the user
            // taps the notification.
            if (!string.IsNullOrEmpty(filePath))
                intent.PutExtra(ExtraFilePath, filePath);

            var pendingIntent = PendingIntent.GetActivity(context, 0, intent,
                PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

            var builder = new NotificationCompat.Builder(context, ChannelId)
                .SetContentTitle(title)
                .SetContentText(message)
                .SetSmallIcon(Resource.Drawable.abc_btn_colored_material)
                .SetAutoCancel(true)
                .SetContentIntent(pendingIntent)
                .SetPriority(0);

            NotificationManagerCompat.From(context).Notify(NotificationId, builder.Build());
            Debug.WriteLine($"[AndroidNotification] Shown: {title} — {message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AndroidNotification] Failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void EnsureChannel(Context context)
    {
        if (sChannelCreated) return;
        sChannelCreated = true;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(ChannelId, "Transfers",
                NotificationImportance.Default);
            channel.Description = "Text and file transfers received from your PC";
            var manager = (NotificationManager)context.GetSystemService(Context.NotificationService);
            manager?.CreateNotificationChannel(channel);
        }
    }

    /// <summary>
    /// Requests POST_NOTIFICATIONS permission (Android 13+).
    /// </summary>
    public static void RequestPermission()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu) return;

        var activity = Platform.CurrentActivity;
        if (activity == null) return;

        if (HasNotificationPermission()) return;

        activity.RequestPermissions(
            [global::Android.Manifest.Permission.PostNotifications],
            requestCode: 200);
    }
}
