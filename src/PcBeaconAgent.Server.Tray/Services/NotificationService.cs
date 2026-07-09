using Hardcodet.Wpf.TaskbarNotification;
using PcBeaconAgent.Server.Tray.Models;
using PcBeaconAgent.Server.Tray.ViewModels;
using PcBeaconAgent.Server.Tray.Views;
using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace PcBeaconAgent.Server.Tray.Services
{
    /// <summary>
    /// Implementation of <see cref="INotificationService"/> for the tray
    /// host. Owns:
    /// <list type="bullet">
    ///   <item>The <see cref="PinPopupWindow"/> instance and its lifecycle.</item>
    ///   <item>The Win32 <c>SHAppBarMessage</c> interop used to snap the
    ///       popup to the taskbar edge. The P/Invoke lives here as private
    ///       detail — no other type in the project needs it, and keeping
    ///       it local makes the audit surface one file wide.</item>
    ///   <item>The mapping from <see cref="NotificationSeverity"/> to
    ///       Hardcodet <see cref="BalloonIcon"/>. When the Tier 3 "custom
    ///       balloon positioning" work lands and we draw our own
    ///       transient popups, this mapping disappears but the public
    ///       <see cref="INotificationService"/> contract stays.</item>
    /// </list>
    /// </summary>
    internal sealed class NotificationService : INotificationService
    {
        private readonly App mApp;
        private TaskbarIcon? mTaskbarIcon;
        private PinPopupWindow? mActivePopup;

        // ── Win32 interop: taskbar rect query ──────────────────────────

        // APPBARDATA + SHAppBarMessage let us query the actual taskbar
        // rectangle (including auto-hidden taskbars) instead of guessing
        // from SystemParameters.WorkArea, which gives the work area AFTER
        // subtracting the taskbar but does not tell us where the taskbar
        // actually is. This matters for bottom / top / left / right
        // taskbar positions and for multi-monitor setups.
        [StructLayout(LayoutKind.Sequential)]
        private struct APPBARDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public IntPtr lParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

        private const uint ABM_GETTASKBARPOS = 0x5;

        // Small gap between the popup and the taskbar / screen edge so
        // the popup doesn't visually touch either. Pixels, device units.
        private const double Margin = 4;

        public NotificationService(App app)
        {
            mApp = app;
        }

        /// <inheritdoc />
        public void AttachTaskbarIcon(TaskbarIcon icon)
        {
            mTaskbarIcon = icon;
        }

        /// <inheritdoc />
        public void ShowPinPopup(string pin, DateTime expiryUtc)
        {
            if (!mApp.Dispatcher.CheckAccess())
            {
                mApp.Dispatcher.Invoke(() => ShowPinPopup(pin, expiryUtc));
                return;
            }

            ClosePinPopup();

            var vm = new PinPopupViewModel(pin, expiryUtc);
            mActivePopup = new PinPopupWindow(vm);
            mActivePopup.Closed += OnPopupClosed;

            // Position before Show() so we don't see a flash at (0,0).
            ApplyPosition(mActivePopup, declaredSizeOnly: true);
            mActivePopup.Show();

            // Re-apply after the visual tree has measured — at Show()
            // time ActualWidth/Height are still 0/NaN.
            mActivePopup.ContentRendered += (_, _) => ApplyPosition(mActivePopup, declaredSizeOnly: false);
        }

        /// <inheritdoc />
        public void ClosePinPopup()
        {
            if (!mApp.Dispatcher.CheckAccess())
            {
                mApp.Dispatcher.Invoke(ClosePinPopup);
                return;
            }

            if (mActivePopup != null)
            {
                // Detach first so our manual Close() doesn't re-enter
                // OnPopupClosed — keeps the lifecycle explicit.
                mActivePopup.Closed -= OnPopupClosed;
                mActivePopup.Close();
                mActivePopup = null;
            }
        }

        /// <inheritdoc />
        public void ShowTransient(string title, string message, NotificationSeverity severity)
        {
            if (!mApp.Dispatcher.CheckAccess())
            {
                mApp.Dispatcher.Invoke(() => ShowTransient(title, message, severity));
                return;
            }

            if (mTaskbarIcon == null) return;

            BalloonIcon icon = severity switch
            {
                NotificationSeverity.Info => BalloonIcon.Info,
                NotificationSeverity.Warning => BalloonIcon.Warning,
                NotificationSeverity.Error => BalloonIcon.Error,
                _ => BalloonIcon.None
            };
            // Hardcodet 2.x dropped the customTimeout parameter — Windows
            // ignores it anyway and clamps to its own system timeout
            // (~10–30s). The balloon is intentionally a short transient
            // signal, not a persistent state holder — that's what the
            // popup is for.
            mTaskbarIcon.ShowBalloonTip(title, message, icon);
        }

        private void OnPopupClosed(object? sender, EventArgs e)
        {
            mActivePopup = null;
        }

        // ── Positioning ────────────────────────────────────────────────

        /// <summary>
        /// Places the popup directly above the taskbar (or against the
        /// relevant screen edge for non-bottom taskbars) with a tiny gap.
        /// Falls back to <see cref="SystemParameters.WorkArea"/> when the
        /// taskbar rect query fails (e.g. on Wine/ReactOS or unusual
        /// shell replacements).
        /// </summary>
        /// <param name="popup">The window to position.</param>
        /// <param name="declaredSizeOnly">
        /// <c>true</c> — use the XAML-declared <c>Width</c>/<c>Height</c>
        /// (called before <c>Show()</c> when <c>ActualWidth</c> is 0).
        /// <c>false</c> — use <c>ActualWidth</c>/<c>ActualHeight</c>
        /// (called from <c>ContentRendered</c> after measure).
        /// </param>
        private void ApplyPosition(Window popup, bool declaredSizeOnly)
        {
            double width, height;
            if (declaredSizeOnly)
            {
                width = popup.Width;
                height = popup.Height;
                if (double.IsNaN(width)) width = 300;
                if (double.IsNaN(height)) height = 160;
            }
            else
            {
                width = popup.ActualWidth > 0 ? popup.ActualWidth : popup.Width;
                height = popup.ActualHeight > 0 ? popup.ActualHeight : popup.Height;
                if (double.IsNaN(width)) width = 300;
                if (double.IsNaN(height)) height = 160;
            }

            Rect workArea = SystemParameters.WorkArea;
            double left = workArea.Right - width - Margin;
            double top = workArea.Bottom - height;

            try
            {
                APPBARDATA abd = new()
                {
                    cbSize = Marshal.SizeOf<APPBARDATA>()
                };

                if (SHAppBarMessage(ABM_GETTASKBARPOS, ref abd) != IntPtr.Zero)
                {
                    RECT taskbar = abd.rc;
                    // ABE_BOTTOM = 0, ABE_TOP = 1, ABE_LEFT = 2, ABE_RIGHT = 3.
                    // The uEdge field is set by the shell, but a coordinate
                    // check is more robust across shell revisions.
                    if (taskbar.Top >= workArea.Bottom - 1)
                    {
                        // Bottom taskbar — popup sits just above it.
                        top = taskbar.Top - height;
                    }
                    else if (taskbar.Bottom <= workArea.Top + 1)
                    {
                        // Top taskbar — popup sits just below it.
                        top = taskbar.Bottom;
                    }
                    else if (taskbar.Right <= workArea.Left + 1)
                    {
                        // Left taskbar.
                        left = taskbar.Right + Margin;
                        top = workArea.Bottom - height;
                    }
                    else if (taskbar.Left >= workArea.Right - 1)
                    {
                        // Right taskbar.
                        left = taskbar.Left - width - Margin;
                        top = workArea.Bottom - height;
                    }
                }
            }
            catch
            {
                // SHAppBarMessage can fail on alternative shells. The
                // workArea-based fallback above already gives a reasonable
                // position, so swallow the exception.
            }

            popup.Left = left;
            popup.Top = top;
        }
    }
}
