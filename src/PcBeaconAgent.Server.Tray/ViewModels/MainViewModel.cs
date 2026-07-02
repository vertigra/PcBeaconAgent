using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcBeaconAgent.Server.Core.Interfaces;

namespace PcBeaconAgent.Server.Tray.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IPairingService mPairingService;

    [ObservableProperty]
    public partial string CurrentPin { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasActivePin { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public MainViewModel(IPairingService pairingService)
    {
        mPairingService = pairingService;
        RefreshPin();
    }

    [RelayCommand]
    public void RegeneratePin()
    {
        IsBusy = true;
        try
        {
            mPairingService.RegeneratePin();
            RefreshPin();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void RefreshPin()
    {
        CurrentPin = mPairingService.GetCurrentPin();
        HasActivePin = mPairingService.IsPairingActive;
    }
}
