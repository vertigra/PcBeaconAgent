using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Android.ViewModels;

namespace PcBeaconAgent.Client.Android;

public partial class SettingsPage : ContentPage
{
    // FIX: храним ссылку на ViewModel, чтобы вызвать обновление из OnAppearing —
    // BindingContext доступен только как object, явная ссылка с правильным типом
    // удобнее и не требует приведения типов.
    private readonly SettingsViewModel mViewModel;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = mViewModel = viewModel;
    }

    // FIX (новый метод): Shell создаёт страницу вкладки один раз и держит её
    // в памяти между переключениями — конструктор SettingsViewModel выполняется
    // только при первом открытии вкладки. Без OnAppearing список StoredKeys
    // навсегда оставался бы таким, каким был в момент первого визита, и не
    // подхватывал бы ключи, добавленные после (например, через паринг нового
    // устройства), пока приложение не будет перезапущено целиком.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        mViewModel.RefreshStoredKeys();
    }
}