using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Android.ViewModels;

namespace PcBeaconAgent.Client.Android.Pages;

public partial class PairingPage : ContentPage
{
    private readonly PairingViewModel mViewModel;

    public PairingPage(PairingViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = mViewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // QueryProperty attributes populate ServerIp/ServerPort before
        // OnAppearing fires, so the view model already knows where to
        // send the regenerate request. The call is fire-and-forget from
        // the page's perspective — the VM handles its own IsBusy state.
        await mViewModel.OnAppearingAsync();
    }
}