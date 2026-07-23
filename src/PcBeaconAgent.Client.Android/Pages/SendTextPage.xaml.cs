using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Android.ViewModels;

namespace PcBeaconAgent.Client.Android.Pages;

/// <summary>
/// Page for composing and sending a text transfer to a managed PC.
/// Replaces the old single-line <c>DisplayPromptAsync</c> approach
/// with a full multiline Editor + Paste + Send UX.
/// </summary>
public partial class SendTextPage : ContentPage
{
    private readonly SendTextViewModel mViewModel;

    public SendTextPage(SendTextViewModel viewModel)
    {
        InitializeComponent();
        mViewModel = viewModel;
        BindingContext = mViewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Consume any pending shared text from the Android Share Sheet
        // hand-off. The VM clears the static after consuming.
        mViewModel.OnPageAppearing();
    }
}
