using PcBeaconAgent.Server.Tray.ViewModels;
using System.Windows;

namespace PcBeaconAgent.Server.Tray.Views
{
    /// <summary>
    /// Main application window — TabControl with three tabs (Pairing,
    /// Settings, Files) and a header showing the app icon, name, and
    /// version. The window stays open until the user closes it; the
    /// tray host itself keeps running.
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // Refresh the Pairing tab whenever the window is shown so
            // the PIN display is not stale. Loaded fires on first show
            // and on every subsequent Show() after Close() — but since
            // we re-use the same window instance across Show/Hide
            // cycles (see TrayViewModel.ShowWindow), Loaded only fires
            // once per window instance. TrayViewModel.ShowWindow calls
            // vm.OnWindowShown() explicitly on each open to cover the
            // reuse case.
            Loaded += (_, _) =>
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.OnWindowShown();
                }
            };
        }
    }
}
