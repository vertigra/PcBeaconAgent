using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Android.ViewModels;

namespace PcBeaconAgent.Client.Android.Pages;

/// <summary>
/// Modal page styled as a bottom sheet. Reached when the user shares
/// a file into the app via the Android Share Sheet. The dark overlay
/// above the card is tap-to-dismiss; the card lists online managed
/// devices and sends the shared file on tap.
/// </summary>
public partial class ShareFilePage : ContentPage
{
    private readonly ShareFileViewModel mViewModel;

    public ShareFilePage(ShareFileViewModel viewModel)
    {
        InitializeComponent();
        mViewModel = viewModel;
        BindingContext = mViewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        mViewModel.OnPageAppearing();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Dispose the VM to unsubscribe from DeviceStatusChanged.
        // Same leak-prevention pattern as ShareTextPage.
        mViewModel.Dispose();
    }
}
