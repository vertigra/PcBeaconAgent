using System.Windows.Controls;

namespace PcBeaconAgent.Server.Tray.Views
{
    /// <summary>
    /// Code-behind for <see cref="SettingsView"/>. Pure view.
    /// DataContext is set by <see cref="MainWindow"/> when the tab is
    /// bound to <see cref="ViewModels.MainViewModel.Settings"/>.
    /// </summary>
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }
    }
}
