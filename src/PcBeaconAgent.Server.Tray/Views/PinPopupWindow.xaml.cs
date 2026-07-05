using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PcBeaconAgent.Server.Tray.Views
{
    /// <summary>
    /// Transient popup that shows the current pairing PIN with a live
    /// countdown. Opened by TrayViewModel on the Generated event; auto-closed
    /// by TrayViewModel on Used/Expired/Locked, or self-closed when the
    /// countdown reaches zero. Stays Topmost so the user can read the PIN
    /// even while switching to the phone.
    /// </summary>
    public partial class PinPopupWindow : Window
    {
        private readonly DateTime mExpiryUtc;
        private readonly TimeSpan mLifetime;
        private readonly DispatcherTimer mTimer;

        /// <param name="pin">The PIN string to display.</param>
        /// <param name="expiryUtc">UTC instant at which the PIN becomes invalid.</param>
        public PinPopupWindow(string pin, DateTime expiryUtc)
        {
            InitializeComponent();

            PinText.Text = pin;
            mExpiryUtc = expiryUtc;
            mLifetime = expiryUtc - DateTime.UtcNow;

            PositionNearTray();

            mTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            mTimer.Tick += OnTimerTick;
            mTimer.Start();

            UpdateCountdown();
        }

        /// <summary>
        /// Places the popup at the bottom-right of the working area
        /// (the screen minus the taskbar). This is the closest stable
        /// position to the system tray on the default Windows taskbar
        /// layout; on side-docked taskbars it's still a reasonable corner.
        /// </summary>
        private void PositionNearTray()
        {
            Rect workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 16;
            Top = workArea.Bottom - Height - 16;
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            UpdateCountdown();

            if (DateTime.UtcNow >= mExpiryUtc)
            {
                mTimer.Stop();
                Close();
            }
        }

        private void UpdateCountdown()
        {
            TimeSpan remaining = mExpiryUtc - DateTime.UtcNow;
            if (remaining < TimeSpan.Zero)
                remaining = TimeSpan.Zero;

            // ProgressBar reflects fraction of lifetime elapsed.
            double fraction = mLifetime > TimeSpan.Zero
                ? remaining.TotalSeconds / mLifetime.TotalSeconds
                : 0.0;
            CountdownBar.Value = fraction * 100.0;

            // "m:ss" — 5 minutes fits in one digit, seconds always two.
            RemainingText.Text = $"{(int)remaining.TotalMinutes}:{remaining.Seconds:D2} remaining";

            // Tint the bar red in the final 30 seconds to add urgency.
            if (remaining <= TimeSpan.FromSeconds(30))
            {
                CountdownBar.Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xB8, 0xB8));
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(PinText.Text);
                CopyButton.Content = "Copied!";
                // Revert after a short delay so the user gets feedback but
                // the button doesn't get stuck in the "Copied!" state.
                var resetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                resetTimer.Tick += (_, _) =>
                {
                    CopyButton.Content = "Copy PIN";
                    resetTimer.Stop();
                };
                resetTimer.Start();
            }
            catch
            {
                // Clipboard can fail in edge cases (locked, no OLESTA).
                // Non-fatal — the PIN is still visible in the popup.
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            // Lets the user drag the popup out of the way if it covers
            // something they need to see. We don't enforce a drag-grip
            // area — the whole window is draggable.
            try { DragMove(); }
            catch { /* DragMove throws if called outside a mouse-down flow */ }
        }

        protected override void OnClosed(EventArgs e)
        {
            mTimer.Stop();
            base.OnClosed(e);
        }
    }
}
