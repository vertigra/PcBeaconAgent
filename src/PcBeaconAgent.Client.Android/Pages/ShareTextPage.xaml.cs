using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Android.ViewModels;

namespace PcBeaconAgent.Client.Android.Pages;

/// <summary>
/// Modal page styled as a bottom sheet. Reached when the user shares
/// text into the app via the Android Share Sheet. The dark overlay
/// above the card is tap-to-dismiss; the card lists online managed
/// devices and sends the shared text on tap.
/// </summary>
public partial class ShareTextPage : ContentPage
{
    private readonly ShareTextViewModel mViewModel;

    public ShareTextPage(ShareTextViewModel viewModel)
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
        // ShareTextViewModel is Transient — each page opening creates
        // a new instance. Without Dispose, every opening leaks a
        // subscription, and RefreshDevices would be called N times
        // per status change after N openings.
        mViewModel.Dispose();
    }
}
