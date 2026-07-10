using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Android.ViewModels;

namespace PcBeaconAgent.Client.Android.Pages;

public partial class AudioControlPage : ContentPage
{
    private readonly AudioControlViewModel mViewModel;

    public AudioControlPage(AudioControlViewModel viewModel)
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
