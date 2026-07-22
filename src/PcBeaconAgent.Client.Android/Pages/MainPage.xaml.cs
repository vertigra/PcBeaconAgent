using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Android.ViewModels;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Android.Pages
{
    public partial class MainPage : ContentPage
    {
        private readonly MainViewModel mViewModel;

        public MainPage(MainViewModel viewModel)
        {
            InitializeComponent();
            mViewModel = viewModel;
            BindingContext = mViewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Check for pending share text on every MainPage appearance.
            // This is the most reliable trigger point — MainPage is the
            // first page that appears when a new AppShell is created
            // (after cold start, activity recreation, or Finish()).
            //
            // App.OnStart / App.OnResume proved unreliable when the
            // process was alive but the activity was recreated (Finish()
            // destroys the activity, process stays alive, new share
            // creates a new activity in the same process — MAUI's
            // Application lifecycle callbacks didn't fire consistently
            // in that scenario, but MainPage.OnAppearing always fires).
            if (!string.IsNullOrEmpty(ShareTextViewModel.PendingSharedText))
            {
                // Small delay to let the Shell finish its initial
                // navigation to MainPage before we push ShareTextPage
                // on top of it. Without this, GoToAsync can race with
                // the Shell's own initialisation and silently fail.
                await Task.Delay(200);
                await Shell.Current.GoToAsync($"///{nameof(ShareTextPage)}");
            }
        }
    }
}
