using PcBeaconAgent.Server.Tray.ViewModels;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;

namespace PcBeaconAgent.Server.Tray.Views
{
    /// <summary>
    /// Transient popup that shows the current pairing PIN with a live
    /// countdown. All display state is driven by <see cref="PinPopupViewModel"/>
    /// via bindings; this code-behind handles only view-side concerns:
    /// positioning near the tray, drag-move, close button, and forwarding
    /// the ViewModel's Expired event to Window.Close().
    /// </summary>
    public partial class PinPopupWindow : Window
    {
        private readonly PinPopupViewModel mViewModel;

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

        public PinPopupWindow(PinPopupViewModel viewModel)
        {
            InitializeComponent();
            mViewModel = viewModel;
            DataContext = mViewModel;

            // Auto-close when the ViewModel signals expiry.
            mViewModel.Expired += Close;

            // Position before Show() so we don't see a flash at (0,0).
            PositionAboveTaskbar();

            // Positioning must be re-applied after the visual tree has
            // measured — otherwise Width/Height are still NaN/0 and the
            // math falls back to defaults.
            ContentRendered += (_, _) => PositionAboveTaskbar();
        }

        /// <summary>
        /// Places the popup directly above the taskbar (or against the
        /// relevant screen edge for non-bottom taskbars) with a tiny gap.
        /// Falls back to <see cref="SystemParameters.WorkArea"/> when the
        /// taskbar rect query fails (e.g. on Wine/ReactOS or unusual
        /// shell replacements).
        /// </summary>
        private void PositionAboveTaskbar()
        {
            double width = ActualWidth > 0 ? ActualWidth : Width;
            double height = ActualHeight > 0 ? ActualHeight : Height;
            if (double.IsNaN(width)) width = 300;
            if (double.IsNaN(height)) height = 200;

            Rect workArea = SystemParameters.WorkArea;
            double left = workArea.Right - width - 4;
            double top = workArea.Bottom - height;

            // Try to get the actual taskbar rect. If the taskbar is
            // auto-hidden, workArea already covers its position; if it's
            // always-visible, workArea excludes it but doesn't expose the
            // gap. SHAppBarMessage gives us the precise bounds so we can
            // snap right above it.
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
                    // The uEdge field is set by the shell, but for our
                    // purposes a coordinate check is more robust.
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
                        // Left taskbar — popup sits just right of it,
                        // bottom-aligned with the work area.
                        left = taskbar.Right + 4;
                        top = workArea.Bottom - height;
                    }
                    else if (taskbar.Left >= workArea.Right - 1)
                    {
                        // Right taskbar — popup sits just left of it.
                        left = taskbar.Left - width - 4;
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

            Left = left;
            Top = top;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
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
            mViewModel.Expired -= Close;
            mViewModel.Dispose();
            base.OnClosed(e);
        }
    }
}
