using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;

namespace PcBeaconAgent.Server.Tray.Services
{
    /// <summary>
    /// HKCU-based implementation of <see cref="IAutoStartService"/>. The
    /// auto-start entry value is the full path to the current executable,
    /// quoted and prefixed with <c>"&lt;path&gt;"</c> so paths with
    /// spaces survive the registry round-trip. We do not pass any
    /// arguments — the tray host starts in its default UI mode.
    /// </summary>
    internal sealed class AutoStartService : IAutoStartService
    {
        /// <summary>
        /// Subkey path under HKCU where Windows reads per-user auto-start
        /// entries. Documented at
        /// <see href="https://learn.microsoft.com/windows/win32/setupapi/run-and-runonce-registry-keys"/>.
        /// </summary>
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        /// <summary>
        /// Name of the registry value we own. Namespaced with the
        /// product name to avoid collisions with other applications
        /// that might register themselves under a generic name.
        /// </summary>
        private const string ValueName = "PcBeaconAgent.Server.Tray";

        public bool IsEnabled
        {
            get
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
                if (key == null) return false;
                return key.GetValue(ValueName) != null;
            }
        }

        public bool SetEnabled(bool enabled)
        {
            try
            {
                if (enabled)
                {
                    // CreateKey opens the subkey for write if it exists,
                    // creates it otherwise. HKCU\...\Run always exists
                    // on a normal Windows install, but CreateKey handles
                    // both cases without us having to branch.
                    using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
                    string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                    {
                        // MainModule can be null on some hosting
                        // configurations (single-file publish pre-.NET 6
                        // bundle, alternative hosts). Fall back to
                        // AppContext.BaseDirectory + assembly name — not
                        // perfect, but covers the single-file publish
                        // case where MainModule is unreliable.
                        exePath = Environment.ProcessPath;
                    }
                    if (string.IsNullOrEmpty(exePath))
                    {
                        return false;
                    }

                    // Quote the path so spaces in the user's profile
                    // path (C:\Program Files\..., or worse, usernames
                    // with spaces) don't break the command line.
                    key.SetValue(ValueName, $"\"{exePath}\"", RegistryValueKind.String);
                    return true;
                }
                else
                {
                    using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
                    if (key == null) return true; // nothing to delete
                    if (key.GetValue(ValueName) == null) return true;
                    key.DeleteValue(ValueName, throwOnMissingValue: false);
                    return true;
                }
            }
            catch (UnauthorizedAccessException)
            {
                // HKCU should always be writable by the current user,
                // but group policy can lock it down. Surface as a
                // soft failure — the rest of the app still works.
                return false;
            }
            catch (Exception)
            {
                // Any other registry failure (e.g. the Run key was
                // deleted by an overzealous registry cleaner) — same
                // treatment: soft failure.
                return false;
            }
        }
    }
}
