using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcBeaconAgent.Server.Core.Interfaces;
using System.Linq;

namespace PcBeaconAgent.Server.Tray.ViewModels;

/// <summary>
/// ViewModel for the tray icon context menu and double-click actions.
/// Holds commands that are bound to the TaskbarIcon's ContextMenu in XAML.
/// </summary>
public partial class TrayViewModel : ObservableObject
{
    private readonly IPairingService mPairingService;
    private readonly App mApp;

    public TrayViewModel(IPairingService pairingService, App app)
    {
        mPairingService = pairingService;
        mApp = app;
    }

    [RelayCommand]
    public void ShowPin()
    {
        var mainWindow = mApp.Windows.OfType<Views.MainWindow>().FirstOrDefault();
        if (mainWindow == null)
        {
            var viewModel = new MainViewModel(mPairingService);
            mainWindow = new Views.MainWindow { DataContext = viewModel };
        }

        if (mainWindow.DataContext is MainViewModel vm)
            vm.RefreshPin();

        mainWindow.Show();
        mainWindow.Activate();
    }

    [RelayCommand]
    public void RegeneratePin()
    {
        mPairingService.RegeneratePin();
        ShowPin();
    }

    [RelayCommand]
    public void Exit()
    {
        mApp.Shutdown(0);
    }
}
