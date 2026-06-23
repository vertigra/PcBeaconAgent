using Microsoft.Maui.Controls;

namespace PcBeaconAgent.Client.Android
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // PairingPage регистрируется здесь, а не в XAML, потому что это
            // модальный/навигационный маршрут с query-параметрами, а не постоянная вкладка.
            Routing.RegisterRoute(nameof(PairingPage), typeof(PairingPage));
        }
    }
}
