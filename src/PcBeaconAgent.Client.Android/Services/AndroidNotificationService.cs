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

public static class AndroidNotificationService
{
    private const string ChannelId = "transfers";
    private const int NotificationId = 1001;
    private static bool sChannelCreated;

    public const string ExtraOpenReceivedTab = "extra_open_received_tab";
    public const string ExtraFilePath = "extra_file_path";

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

    public static void ShowNotification(string title, string message, string? filePath = null)
    {
        var context = Platform.CurrentActivity ?? Application.Context;
        if (context == null) return;
        if (!HasNotificationPermission()) return;

        EnsureChannel(context);

        try
        {
            // Main tap — opens Received tab.
            var mainIntent = new Intent(context, typeof(MainActivity));
            mainIntent.AddFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
            mainIntent.PutExtra(ExtraOpenReceivedTab, true);

            var mainPending = PendingIntent.GetActivity(context, 0, mainIntent,
                PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

            var builder = new NotificationCompat.Builder(context, ChannelId)
                .SetContentTitle(title)
                .SetContentText(message)
                .SetSmallIcon(Resource.Drawable.abc_btn_colored_material)
                .SetAutoCancel(true)
                .SetContentIntent(mainPending)
                .SetPriority(0);

            // "Open folder" action button (file only).
            if (!string.IsNullOrEmpty(filePath))
            {
                var folderIntent = new Intent(context, typeof(MainActivity));
                folderIntent.AddFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
                folderIntent.PutExtra(ExtraFilePath, filePath);

                var folderPending = PendingIntent.GetActivity(context, 1, folderIntent,
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

                builder.AddAction(0, "Open folder", folderPending);
            }

            NotificationManagerCompat.From(context).Notify(NotificationId, builder.Build());
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

    /// <summary>
    /// Checks intent for notification extras. Called from BOTH
    /// OnCreate (cold start) and OnNewIntent (app running).
    /// Returns true if consumed.
    /// </summary>
    public static bool HandleNotificationIntent(Intent? intent)
    {
        if (intent == null) return false;

        // "Open folder" action button.
        string? filePath = intent.GetStringExtra(ExtraFilePath);
        if (!string.IsNullOrEmpty(filePath))
        {
            intent.RemoveExtra(ExtraFilePath);
            _ = MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    var ctx = Platform.CurrentActivity ?? Application.Context;
                    if (ctx == null) return;

                    string folder = System.IO.Path.GetDirectoryName(filePath) ?? filePath;
                    var file = new Java.IO.File(folder);
                    var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(
                        ctx, ctx.PackageName + ".fileprovider", file);

                    var view = new Intent(Intent.ActionView);
                    view.SetDataAndType(uri, "resource/folder");
                    view.AddFlags(ActivityFlags.GrantReadUriPermission);
                    view.AddFlags(ActivityFlags.NewTask);
                    ctx.StartActivity(Intent.CreateChooser(view, "Open folder"));
                }
                catch { }
            });
            return true;
        }

        // Tap on notification body — open Received tab.
        bool openReceived = intent.GetBooleanExtra(ExtraOpenReceivedTab, false);
        if (openReceived)
        {
            intent.RemoveExtra(ExtraOpenReceivedTab);
            _ = MainThread.InvokeOnMainThreadAsync(() =>
            {
                try { Microsoft.Maui.Controls.Shell.Current.GoToAsync("//ReceivedPage"); }
                catch { }
            });
            return true;
        }

        return false;
    }
}
