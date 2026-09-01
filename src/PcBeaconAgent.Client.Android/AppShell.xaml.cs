using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Android.Pages;

namespace PcBeaconAgent.Client.Android
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Register all non-tab routes. Tab pages (MainPage,
            // ReceivedPage, DiscoveryPage, SettingsPage) are registered
            // via ShellContent in AppShell.xaml. Non-tab pages are
            // pushed onto the navigation stack and must be registered
            // here by name — MAUI resolves GoToAsync("PageName") to
            // the registered type.
            Routing.RegisterRoute(nameof(PairingPage), typeof(PairingPage));
            Routing.RegisterRoute(nameof(AudioControlPage), typeof(AudioControlPage));
            Routing.RegisterRoute(nameof(DisplayControlPage), typeof(DisplayControlPage));
            Routing.RegisterRoute(nameof(SendTextPage), typeof(SendTextPage));
            Routing.RegisterRoute(nameof(ShareTextPage), typeof(ShareTextPage));
            Routing.RegisterRoute(nameof(ShareFilePage), typeof(ShareFilePage));
            Routing.RegisterRoute(nameof(AppsPage), typeof(AppsPage));
        }
    }
}
