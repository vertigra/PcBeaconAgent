using Microsoft.Maui.Controls;

namespace PcBeaconAgent.Client.Android
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(PairingPage), typeof(PairingPage));
            Routing.RegisterRoute(nameof(AudioControlPage), typeof(AudioControlPage));
        }
    }
}
