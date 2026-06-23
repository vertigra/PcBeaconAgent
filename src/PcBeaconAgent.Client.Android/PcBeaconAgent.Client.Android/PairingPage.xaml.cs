using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Android.ViewModels;

namespace PcBeaconAgent.Client.Android;

public partial class PairingPage : ContentPage
{
    public PairingPage(PairingViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}