using PcBeaconAgent.Server.Tray.Services;
using PcBeaconAgent.Server.Tray.ViewModels;
using System;
using System.Windows;
using System.Windows.Input;

namespace PcBeaconAgent.Server.Tray.Views
{
    /// <summary>
    /// Transient popup that shows the current pairing PIN with a live
    /// countdown. All display state is driven by
    /// <see cref="PinPopupViewModel"/> via bindings; this code-behind
    /// handles only view-side concerns: setting the DataContext,
    /// positioning near the tray (via <see cref="TaskbarPositioner"/>),
    /// drag-move, the close button, and forwarding the ViewModel's
    /// <see cref="PinPopupViewModel.Expired"/> event to <see cref="Window.Close"/>.
    /// No P/Invoke lives here.
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
            Position();
            // Position must be re-applied after the visual tree has
            // measured — at constructor time ActualWidth/Height are 0.
            ContentRendered += (_, _) => Position();
        }

        private void Position()
        {
            double width = ActualWidth > 0 ? ActualWidth : Width;
            double height = ActualHeight > 0 ? ActualHeight : Height;
            if (double.IsNaN(width)) width = 300;
            if (double.IsNaN(height)) height = 160;

            Point p = TaskbarPositioner.GetPositionAboveTaskbar(width, height);
            Left = p.X;
            Top = p.Y;
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
