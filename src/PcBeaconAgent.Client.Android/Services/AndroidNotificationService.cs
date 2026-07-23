using Android;
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using Microsoft.Maui.ApplicationModel;
using PcBeaconAgent.Client.Android.Platforms.Android;
using static Android.Manifest;


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
    /// Shows a notification. Tap opens the app.
    /// </summary>
    public static void ShowNotification(string title, string message)
    {
        var context = Platform.CurrentActivity ?? Application.Context;
        if (context == null) return;

        EnsureChannel(context);

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
            .SetPriority(NotificationCompat.PriorityDefault);

        try
        {
            NotificationManagerCompat.From(context).Notify(NotificationId, builder.Build());
        }
        catch
        {
            // SecurityException on Android 13+ if POST_NOTIFICATIONS
            // not granted. Transfer still saved in store — swallow.
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
    /// No-op on older versions.
    /// </summary>
    public static void RequestPermission()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu) return;

        var activity = Platform.CurrentActivity;
        if (activity == null) return;

        //if (activity.CheckSelfPermission(Manifest.Permission.PostNotifications) == Android.Content.PM.Permission.Granted)
            //return;

        activity.RequestPermissions(
            [Permission.PostNotifications],
            requestCode: 200);
    }
}
