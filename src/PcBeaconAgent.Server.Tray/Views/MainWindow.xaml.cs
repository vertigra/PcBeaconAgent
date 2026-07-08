using PcBeaconAgent.Server.Tray.Services;
using PcBeaconAgent.Server.Tray.ViewModels;
using System.Windows;

namespace PcBeaconAgent.Server.Tray.Views
{
    /// <summary>
    /// Main application window — TabControl with three tabs (Pairing,
    /// Settings, Files). The window title carries the app name and
    /// version (set once in the constructor — no binding needed
    /// because the version does not change at runtime).
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Window title bar carries the version — no in-window
            // header needed. AppInfo is the single source of truth
            // shared with SettingsViewModel.About section.
            Title = $"PcBeaconAgent v{AppInfo.Version}";

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
