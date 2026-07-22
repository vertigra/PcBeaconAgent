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
    ///   <item>The persistent <see cref="PinPopupWindow"/> instance and
    ///       its lifecycle (open on Generated, close on terminal
    ///       states).</item>
    ///   <item>The transient <see cref="TransientToastWindow"/> shown for
    ///       terminal pairing states (Used / Expired / Locked). Only one
    ///       toast is on screen at a time — a new toast closes the
    ///       previous one.</item>
    ///   <item>The Win32 <c>SHAppBarMessage</c> interop used to snap both
    ///       surfaces to the taskbar edge. The P/Invoke lives here as
    ///       private detail — no other type in the project needs it, and
    ///       keeping it local makes the audit surface one file wide.</item>
    /// </list>
    /// </summary>
    internal sealed class NotificationService : INotificationService
    {
        private readonly App mApp;
        private PinPopupWindow? mActivePopup;
        private TransientToastWindow? mActiveToast;

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
            ApplyPosition(mActivePopup, declaredSizeOnly: true,
                          fallbackWidth: 300, fallbackHeight: 160);
            mActivePopup.Show();

            // Re-apply after the visual tree has measured — at Show()
            // time ActualWidth/Height are still 0/NaN.
            mActivePopup.ContentRendered += (_, _) => ApplyPosition(
                mActivePopup, declaredSizeOnly: false,
                fallbackWidth: 300, fallbackHeight: 160);
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

            CloseTransient();

            var vm = new TransientToastViewModel(title, message, severity);
            mActiveToast = new TransientToastWindow(vm);
            mActiveToast.Closed += OnToastClosed;

            // Same two-phase positioning as the PIN popup: declared size
            // first (so Show() doesn't flash at 0,0), then re-apply after
            // measure. Fallback height is smaller than the PIN popup
            // because the toast has no countdown / progress bar.
            ApplyPosition(mActiveToast, declaredSizeOnly: true,
                          fallbackWidth: 300, fallbackHeight: 80);
            mActiveToast.Show();

            mActiveToast.ContentRendered += (_, _) => ApplyPosition(
                mActiveToast, declaredSizeOnly: false,
                fallbackWidth: 300, fallbackHeight: 80);
        }

        private void CloseTransient()
        {
            if (mActiveToast != null)
            {
                mActiveToast.Closed -= OnToastClosed;
                mActiveToast.Close();
                mActiveToast = null;
            }
        }

        private void OnPopupClosed(object? sender, EventArgs e)
        {
            mActivePopup = null;
        }

        private void OnToastClosed(object? sender, EventArgs e)
        {
            mActiveToast = null;
        }

        // ── Positioning ────────────────────────────────────────────────

        /// <summary>
        /// Places the window directly above the taskbar (or against the
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
        /// <param name="fallbackWidth">Used when the window has no
        /// declared <c>Width</c> and no measured <c>ActualWidth</c>
        /// (e.g. <c>SizeToContent</c> windows before measure).</param>
        /// <param name="fallbackHeight">Same, for height.</param>
        private void ApplyPosition(Window popup, bool declaredSizeOnly,
                                   double fallbackWidth, double fallbackHeight)
        {
            double width, height;
            if (declaredSizeOnly)
            {
                width = popup.Width;
                height = popup.Height;
                if (double.IsNaN(width)) width = fallbackWidth;
                if (double.IsNaN(height)) height = fallbackHeight;
            }
            else
            {
                width = popup.ActualWidth > 0 ? popup.ActualWidth : popup.Width;
                height = popup.ActualHeight > 0 ? popup.ActualHeight : popup.Height;
                if (double.IsNaN(width)) width = fallbackWidth;
                if (double.IsNaN(height)) height = fallbackHeight;
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
