using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Android.ViewModels;

namespace PcBeaconAgent.Client.Android.Pages;

/// <summary>
/// Page showing the list of launchers configured on the server.
/// Tapping a launcher sends a launch request via HTTP.
/// </summary>
public partial class AppsPage : ContentPage
{
    private readonly AppsViewModel mViewModel;

    public AppsPage(AppsViewModel viewModel)
    {
        InitializeComponent();
        mViewModel = viewModel;
        BindingContext = mViewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await mViewModel.LoadAsync();
    }
}
