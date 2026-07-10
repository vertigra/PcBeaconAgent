using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Android.ViewModels;

namespace PcBeaconAgent.Client.Android.Pages;

public partial class DisplayControlPage : ContentPage
{
    private readonly DisplayControlViewModel mViewModel;

    public DisplayControlPage(DisplayControlViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = mViewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await mViewModel.LoadAsync();
    }
}