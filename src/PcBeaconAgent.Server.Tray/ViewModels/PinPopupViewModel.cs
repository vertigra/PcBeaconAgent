using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Windows.Threading;

namespace PcBeaconAgent.Server.Tray.ViewModels
{
    /// <summary>
    /// ViewModel for <see cref="Views.PinPopupWindow"/>. Owns the countdown
    /// timer and the display state — the view is pure XAML with bindings,
    /// no code-behind beyond InitializeComponent / position / drag-move
    /// (which are inherently view concerns and don't belong here).
    /// </summary>
    public partial class PinPopupViewModel : ObservableObject, IDisposable
    {
        private readonly DateTime mExpiryUtc;
        private readonly TimeSpan mLifetime;
        private readonly DispatcherTimer mTimer;
        private bool mDisposed;

        [ObservableProperty]
        public partial string Pin { get; set; }

        [ObservableProperty]
        public partial string RemainingText { get; set; } = "5:00 remaining";

        [ObservableProperty]
        public partial double CountdownPercent { get; set; } = 100.0;

        [ObservableProperty]
        public partial bool IsUrgent { get; set; }

        /// <summary>
        /// Raised when the countdown reaches zero. The window subscribes
        /// to close itself — keeping the close decision in the view because
        /// <c>Window.Close()</c> is a view-side concern.
        /// </summary>
        public event Action? Expired;

        /// <param name="pin">PIN string to display.</param>
        /// <param name="expiryUtc">UTC instant at which the PIN becomes invalid.</param>
        public PinPopupViewModel(string pin, DateTime expiryUtc)
        {
            Pin = pin;
            mExpiryUtc = expiryUtc;
            mLifetime = expiryUtc - DateTime.UtcNow;

            mTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            mTimer.Tick += OnTimerTick;
            mTimer.Start();

            UpdateCountdown();
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            UpdateCountdown();

            if (DateTime.UtcNow >= mExpiryUtc)
            {
                mTimer.Stop();
                Expired?.Invoke();
            }
        }

        private void UpdateCountdown()
        {
            TimeSpan remaining = mExpiryUtc - DateTime.UtcNow;
            if (remaining < TimeSpan.Zero)
                remaining = TimeSpan.Zero;

            double fraction = mLifetime > TimeSpan.Zero
                ? remaining.TotalSeconds / mLifetime.TotalSeconds
                : 0.0;
            CountdownPercent = fraction * 100.0;

            // "m:ss" — 5 minutes fits in one digit, seconds always two.
            RemainingText = $"{(int)remaining.TotalMinutes}:{remaining.Seconds:D2} remaining";

            // Tint the bar red in the final 30 seconds to add urgency.
            IsUrgent = remaining <= TimeSpan.FromSeconds(30);
        }

        public void Dispose()
        {
            if (!mDisposed)
            {
                mTimer.Stop();
                mDisposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}
