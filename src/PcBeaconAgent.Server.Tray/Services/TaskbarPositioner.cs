using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace PcBeaconAgent.Server.Tray.Services
{
    /// <summary>
    /// Isolates the Win32 shell P/Invoke needed to locate the taskbar.
    /// Keeps the unmanaged imports in one well-audited place instead of
    /// spreading them across views — the views call the managed
    /// <see cref="GetPositionAboveTaskbar(double, double)"/> helper and
    /// stay P/Invoke-free.
    /// </summary>
    internal static class TaskbarPositioner
    {
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

        // Small gap between the popup and the taskbar / screen edge, so
        // the popup doesn't visually touch either. Pixels, device units.
        private const double Margin = 4;

        /// <summary>
        /// Returns the screen-space coordinates (Left, Top) at which a
        /// window of size <paramref name="width"/>×<paramref name="height"/>
        /// should be placed so it sits flush against the taskbar (or
        /// against the corresponding screen edge on alt shells where the
        /// SHAppBarMessage call fails).
        /// </summary>
        /// <remarks>
        /// The caller is responsible for setting <c>ActualWidth</c>/
        /// <c>ActualHeight</c> — at <c>Loaded</c> time these may still be
        /// NaN/0, so the caller should fall back to declared <c>Width</c>/
        /// <c>Height</c> and re-call after <c>ContentRendered</c>.
        /// </remarks>
        public static Point GetPositionAboveTaskbar(double width, double height)
        {
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
                        // Left taskbar — popup sits just right of it,
                        // bottom-aligned with the work area.
                        left = taskbar.Right + Margin;
                        top = workArea.Bottom - height;
                    }
                    else if (taskbar.Left >= workArea.Right - 1)
                    {
                        // Right taskbar — popup sits just left of it.
                        left = taskbar.Left - width - Margin;
                        top = workArea.Bottom - height;
                    }
                }
            }
            catch
            {
                // SHAppBarMessage can fail on alternative shells (Wine,
                // ReactOS, explorer replacements). The workArea-based
                // fallback above already gives a reasonable position.
            }

            return new Point(left, top);
        }
    }
}
