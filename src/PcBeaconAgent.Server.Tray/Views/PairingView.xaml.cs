using System.Windows.Controls;

namespace PcBeaconAgent.Server.Tray.Views
{
    /// <summary>
    /// Code-behind for <see cref="PairingView"/>. Pure view — no
    /// positioning, no P/Invoke, no lifecycle logic. The DataContext
    /// is set by the parent <see cref="MainWindow"/> when the tab is
    /// bound to <see cref="ViewModels.MainViewModel.Pairing"/>.
    /// </summary>
    public partial class PairingView : UserControl
    {
        public PairingView()
        {
            InitializeComponent();
        }
    }
}
