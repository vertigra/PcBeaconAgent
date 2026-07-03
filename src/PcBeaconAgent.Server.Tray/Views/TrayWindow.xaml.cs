using System.Windows;

namespace PcBeaconAgent.Server.Tray.Views;

/// <summary>
/// An invisible window that hosts the TaskbarIcon control.
/// The window itself is never shown — it exists solely to provide
/// a WPF lifetime scope and DataContext for the tray icon.
/// </summary>
public partial class TrayWindow : Window
{
    public TrayWindow()
    {
        InitializeComponent();
    }

    private void TrayIcon_TrayMouseDoubleClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.TrayViewModel vm)
            vm.ShowPin();
    }
}
