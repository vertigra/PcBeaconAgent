using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Android.ViewModels;

namespace PcBeaconAgent.Client.Android;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel mViewModel;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = mViewModel = viewModel;
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        mViewModel.RefreshStoredKeys();
    }
}