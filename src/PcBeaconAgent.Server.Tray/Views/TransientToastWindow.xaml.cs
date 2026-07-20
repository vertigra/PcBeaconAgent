using PcBeaconAgent.Server.Tray.ViewModels;
using System;
using System.Windows;
using System.Windows.Input;

namespace PcBeaconAgent.Server.Tray.Views
{
    /// <summary>
    /// Short-lived borderless toast that replaces the Win32
    /// <c>Shell_NotifyIcon</c> balloon for transient terminal-state
    /// notifications (pairing Used / Expired / Locked). Position is set
    /// by <see cref="Services.INotificationService"/> via the same
    /// taskbar-snap logic used for <see cref="PinPopupWindow"/>, so the
    /// two surfaces stay pixel-aligned.
    /// <para>
    /// Click anywhere dismisses the toast immediately; otherwise the
    /// ViewModel's timer closes it after
    /// <see cref="TransientToastViewModel.DefaultDuration"/>.
    /// </para>
    /// </summary>
    public partial class TransientToastWindow : Window
    {
        private readonly TransientToastViewModel mViewModel;

        public TransientToastWindow(TransientToastViewModel viewModel)
        {
            InitializeComponent();
            mViewModel = viewModel;
            DataContext = mViewModel;

            mViewModel.Expired += Close;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            // The toast is non-interactive — any click on it is
            // unambiguously an "I saw it, go away" gesture.
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            mViewModel.Expired -= Close;
            mViewModel.Dispose();
            base.OnClosed(e);
        }
    }
}
