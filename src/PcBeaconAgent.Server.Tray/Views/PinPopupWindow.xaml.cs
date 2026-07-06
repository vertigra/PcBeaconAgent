using PcBeaconAgent.Server.Tray.ViewModels;
using System;
using System.Windows;
using System.Windows.Input;

namespace PcBeaconAgent.Server.Tray.Views
{
    /// <summary>
    /// Transient popup that shows the current pairing PIN with a live
    /// countdown. All display state is driven by
    /// <see cref="PinPopupViewModel"/> via bindings. This code-behind
    /// handles only view-side concerns: setting the DataContext, the
    /// close button, drag-move, and forwarding the ViewModel's
    /// <see cref="PinPopupViewModel.Expired"/> event to
    /// <see cref="Window.Close"/>.
    /// <para>
    /// Positioning is the responsibility of
    /// <see cref="Services.INotificationService"/> — the window itself
    /// does not know or care where on screen it appears.
    /// </para>
    /// </summary>
    public partial class PinPopupWindow : Window
    {
        private readonly PinPopupViewModel mViewModel;

        public PinPopupWindow(PinPopupViewModel viewModel)
        {
            InitializeComponent();
            mViewModel = viewModel;
            DataContext = mViewModel;

            mViewModel.Expired += Close;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            // Lets the user drag the popup out of the way if it covers
            // something they need to see.
            try { DragMove(); }
            catch { /* DragMove throws if called outside a mouse-down flow */ }
        }

        protected override void OnClosed(EventArgs e)
        {
            mViewModel.Expired -= Close;
            mViewModel.Dispose();
            base.OnClosed(e);
        }
    }
}
