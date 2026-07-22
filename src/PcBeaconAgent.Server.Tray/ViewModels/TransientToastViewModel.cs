using CommunityToolkit.Mvvm.ComponentModel;
using PcBeaconAgent.Server.Tray.Models;
using System;
using System.Windows.Threading;

namespace PcBeaconAgent.Server.Tray.ViewModels
{
    /// <summary>
    /// ViewModel for <see cref="Views.TransientToastWindow"/>. Owns the
    /// auto-close timer and raises <see cref="Expired"/> when it fires,
    /// so the window can close itself. Mirrors the lifecycle pattern of
    /// <see cref="PinPopupViewModel"/> but without a countdown UI — the
    /// toast is intentionally short-lived and non-interactive (apart
    /// from click-to-close).
    /// </summary>
    public partial class TransientToastViewModel : ObservableObject, IDisposable
    {
        /// <summary>
        /// Default on-screen duration. Picked to match the lower end of
        /// the old Windows balloon timeout (~5s) — long enough to read
        /// a two-line message, short enough not to linger over whatever
        /// the user was doing.
        /// </summary>
        public static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(5);

        private readonly DispatcherTimer mTimer;
        private bool mDisposed;

        [ObservableProperty]
        public partial string Title { get; set; }

        [ObservableProperty]
        public partial string Message { get; set; }

        [ObservableProperty]
        public partial NotificationSeverity Severity { get; set; }

        /// <summary>
        /// Raised when the auto-close timer fires. The window subscribes
        /// to close itself — keeping the close decision in the view
        /// because <c>Window.Close()</c> is a view-side concern.
        /// </summary>
        public event Action? Expired;

        /// <param name="title">Short one-line title.</param>
        /// <param name="message">Body text, may wrap to two lines.</param>
        /// <param name="severity">Drives the accent stripe colour via
        /// a XAML <c>DataTrigger</c> in
        /// <see cref="Views.TransientToastWindow"/>.</param>
        public TransientToastViewModel(string title, string message, NotificationSeverity severity)
            : this(title, message, severity, DefaultDuration)
        {
        }

        /// <param name="title">Short one-line title.</param>
        /// <param name="message">Body text, may wrap to two lines.</param>
        /// <param name="severity">Drives the accent stripe colour.</param>
        /// <param name="duration">On-screen duration before auto-close.
        /// Exposed for tests so they can pass a sub-second duration and
        /// assert the <see cref="Expired"/> callback without sleeping.</param>
        public TransientToastViewModel(string title, string message, NotificationSeverity severity, TimeSpan duration)
        {
            Title = title;
            Message = message;
            Severity = severity;

            mTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = duration
            };
            mTimer.Tick += OnTimerTick;
            mTimer.Start();
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            mTimer.Stop();
            Expired?.Invoke();
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
