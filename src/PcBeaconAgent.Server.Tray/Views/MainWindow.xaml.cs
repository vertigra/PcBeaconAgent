using PcBeaconAgent.Server.Tray.ViewModels;
using System.Windows;

namespace PcBeaconAgent.Server.Tray.Views
{
    /// <summary>
    /// Main application window — TabControl with three tabs (Pairing,
    /// Settings, Files). The window title is bound to
    /// <see cref="Services.AppInfo.Version"/> via <c>Binding</c> with a
    /// static source in XAML — no code-behind needed.
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
