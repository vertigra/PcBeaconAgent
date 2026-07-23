using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Android.ViewModels;

namespace PcBeaconAgent.Client.Android.Pages;

/// <summary>
/// Page for sending text or a file to a managed PC. The Editor +
/// Paste + Send buttons handle text; the 📎 File button opens the
/// system file picker and sends a file inline.
/// </summary>
public partial class SendTextPage : ContentPage
{
    public SendTextPage(SendTextViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
