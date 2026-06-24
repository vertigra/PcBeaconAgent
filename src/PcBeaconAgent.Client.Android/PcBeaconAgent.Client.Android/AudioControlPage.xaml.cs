using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Android.ViewModels;

namespace PcBeaconAgent.Client.Android;

public partial class AudioControlPage : ContentPage
{
    private readonly AudioControlViewModel mViewModel;

    public AudioControlPage(AudioControlViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = mViewModel = viewModel;
    }

    // FIX: загрузка происходит здесь, а не в конструкторе ViewModel — DeviceIp
    // проставляется Shell'ом через QueryProperty уже ПОСЛЕ создания экземпляра,
    // но ДО OnAppearing (тот же паттерн, что чинили для SettingsPage ранее).
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await mViewModel.LoadAsync();
    }
}