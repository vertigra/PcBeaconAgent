using System;
using Hardcodet.Wpf.TaskbarNotification;

namespace PcBeaconAgent.Server.Tray.Notifications
{
    /// <summary>
    /// Owns the user-facing notification surface for the tray host:
    /// the persistent PIN popup (stateful — opened on Generated, closed
    /// on terminal states) and the transient balloons (Used / Expired /
    /// Locked). Encapsulates Win32 taskbar positioning so view models
    /// and views stay P/Invoke-free.
    /// </summary>
    /// <remarks>
    /// <b>Threading.</b> All methods can be called from any thread —
    /// the implementation marshals to the UI thread internally.
    /// </remarks>
    public interface INotificationService
    {
        /// <summary>
        /// Shows the PIN popup with a live countdown. Closes any popup
        /// that was already open so two popups never coexist.
        /// </summary>
        void ShowPinPopup(string pin, DateTime expiryUtc);

        /// <summary>
        /// Closes the PIN popup if one is open. No-op otherwise.
        /// </summary>
        void ClosePinPopup();

        /// <summary>
        /// Shows a short transient notification (balloon today, custom
        /// WPF popup once Tier 3 lands). Windows clamps the on-screen
        /// duration to ~10–30s; we cannot control it.
        /// </summary>
        void ShowTransient(string title, string message, NotificationSeverity severity);

        /// <summary>
        /// Hands the service the <see cref="TaskbarIcon"/> reference
        /// after the <c>TrayWindow</c> has constructed it. The icon is
        /// declared in XAML, so it is not available at DI resolution
        /// time — the host calls this method once during startup.
        /// </summary>
        void AttachTaskbarIcon(TaskbarIcon icon);
    }
}
