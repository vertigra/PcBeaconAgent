using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Net.Wifi;
using Android.OS;
using Microsoft.Maui;

namespace PcBeaconAgent.Client.Android
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        // Ссылка на наш "замок", чтобы мы могли открыть его при закрытии приложения
        private WifiManager.MulticastLock? _multicastLock;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // 1. Запрашиваем у Android доступ к системному сервису Wi-Fi
            if (GetSystemService(Context.WifiService) is WifiManager wifi)
            {
                // 2. Создаем замок с произвольным текстовым именем-тегом
                _multicastLock = wifi.CreateMulticastLock("pc_beacon_multicast_lock");

                // 3. АКТИВИРУЕМ замок. С этой секунды Wi-Fi чип перестает выбрасывать пакеты ответов!
                _multicastLock?.Acquire();
            }
        }

        protected override void OnDestroy()
        {
            // Когда приложение полностью закрывается, возвращаем настройки Wi-Fi обратно,
            // чтобы не тратить батарею смартфона впустую
            if (_multicastLock is { IsHeld: true })
            {
                _multicastLock.Release();
            }
            base.OnDestroy();
        }
    }
}
