using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Net.Wifi;
using Android.OS;
using Microsoft.Maui;

namespace PcBeaconAgent.Client.Android;

/// <summary>
/// Main lifecycle activity for the Android application.
/// Manages system-level configurations including Wi-Fi multicast locking.
/// </summary>
[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
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