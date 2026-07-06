using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Android.Pages;

namespace PcBeaconAgent.Client.Android
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(PairingPage), typeof(PairingPage));
            Routing.RegisterRoute(nameof(AudioControlPage), typeof(AudioControlPage));
            Routing.RegisterRoute(nameof(DisplayControlPage), typeof(DisplayControlPage));
        }
    }
}
