using System;
using PcBeaconAgent.Server.Tray.Models;

namespace PcBeaconAgent.Server.Tray.Services
{
    /// <summary>
    /// Owns the user-facing notification surface for the tray host:
    /// the persistent PIN popup (stateful — opened on Generated, closed
    /// on terminal states) and the transient toast (Used / Expired /
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
        /// Shows a short transient toast (custom WPF popup). Windows
        /// clamped the old <c>Shell_NotifyIcon</c> balloon to ~10–30s
        /// and positioned it wherever it liked; the custom toast has a
        /// fixed 5s duration and snaps to the taskbar edge, pixel-aligned
        /// with the PIN popup.
        /// </summary>
        void ShowTransient(string title, string message, NotificationSeverity severity);
    }
}
