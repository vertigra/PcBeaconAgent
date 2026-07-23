using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Android.ViewModels;

namespace PcBeaconAgent.Client.Android.Pages;

/// <summary>
/// Shows all transfers received from the PC (text + files), with
/// Copy / Open actions. Reached from the "Received" tab.
/// </summary>
public partial class ReceivedPage : ContentPage
{
    public ReceivedPage(ReceivedViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
