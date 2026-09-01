using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using Microsoft.Maui.ApplicationModel;
using PcBeaconAgent.Client.Android.Platforms.Android;
using System;
using System.Diagnostics;

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
    public static void ShowNotification(string title, string message)
    {
        var context = Platform.CurrentActivity ?? Application.Context;
        if (context == null)
        {
            System.Diagnostics.Debug.WriteLine("[AndroidNotification] Context is null, cannot show notification");
            return;
        }

        // Check permission on Android 13+. If not granted, the
        // notification will silently fail, so skip it and log.
        if (!HasNotificationPermission())
        {
            System.Diagnostics.Debug.WriteLine("[AndroidNotification] POST_NOTIFICATIONS permission not granted — skipping notification");
            return;
        }

        EnsureChannel(context);

        try
        {
            var intent = new Intent(context, typeof(MainActivity));
            intent.AddFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
            var pendingIntent = PendingIntent.GetActivity(context, 0, intent,
                PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

            var builder = new NotificationCompat.Builder(context, ChannelId)
                .SetContentTitle(title)
                .SetContentText(message)
                .SetSmallIcon(Resource.Drawable.abc_btn_colored_material)
                .SetAutoCancel(true)
                .SetContentIntent(pendingIntent)
                .SetPriority(0); // NotificationCompat.PriorityDefault = 0

            NotificationManagerCompat.From(context).Notify(NotificationId, builder.Build());
            System.Diagnostics.Debug.WriteLine($"[AndroidNotification] Notification shown: {title} — {message}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AndroidNotification] Failed to show notification: {ex.GetType().Name}: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine("[AndroidNotification] Channel created");
        }
    }

    /// <summary>
    /// Requests POST_NOTIFICATIONS permission (Android 13+).
    /// No-op on older versions.
    /// </summary>
    public static void RequestPermission()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu)
        {
            System.Diagnostics.Debug.WriteLine("[AndroidNotification] API < 33, permission not required");
            return;
        }

        var activity = Platform.CurrentActivity;
        if (activity == null)
        {
            System.Diagnostics.Debug.WriteLine("[AndroidNotification] Activity is null, cannot request permission");
            return;
        }

        if (HasNotificationPermission())
        {
            System.Diagnostics.Debug.WriteLine("[AndroidNotification] Permission already granted");
            return;
        }

        System.Diagnostics.Debug.WriteLine("[AndroidNotification] Requesting POST_NOTIFICATIONS permission...");
        activity.RequestPermissions(
            [global::Android.Manifest.Permission.PostNotifications],
            requestCode: 200);
    }
}
