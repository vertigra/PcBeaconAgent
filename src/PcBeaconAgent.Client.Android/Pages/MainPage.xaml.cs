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

            // Check for pending share payloads on every MainPage
            // appearance. This is the most reliable trigger point —
            // MainPage is the first page that appears when a new
            // AppShell is created (after cold start, activity
            // recreation, or Finish()).
            //
            // App.OnStart / App.OnResume proved unreliable when the
            // process was alive but the activity was recreated.
            // MainPage.OnAppearing always fires.
            //
            // Text shares stash into ShareTextViewModel.PendingSharedText;
            // file shares stash into ShareFileViewModel.PendingFileUri.
            // We check text first (matches ConsumeShareIntent's priority).
            if (!string.IsNullOrEmpty(ShareTextViewModel.PendingSharedText))
            {
                await Task.Delay(200);
                await Shell.Current.GoToAsync($"///{nameof(ShareTextPage)}");
            }
            else if (ShareFileViewModel.PendingFileUri != null)
            {
                await Task.Delay(200);
                await Shell.Current.GoToAsync($"///{nameof(ShareFilePage)}");
            }
        }
    }
}
