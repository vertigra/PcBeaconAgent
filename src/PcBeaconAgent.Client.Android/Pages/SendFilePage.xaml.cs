using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Android.ViewModels;

namespace PcBeaconAgent.Client.Android.Pages;

/// <summary>
/// Page for picking and sending a file to a managed PC. Reached from
/// the send-file button on a device card. Uses MAUI's FilePicker API
/// for system file selection, then streams the file to the server.
/// </summary>
public partial class SendFilePage : ContentPage
{
    private readonly SendFileViewModel mViewModel;

    public SendFilePage(SendFileViewModel viewModel)
    {
        InitializeComponent();
        mViewModel = viewModel;
        BindingContext = mViewModel;
    }
}
