using System;
using System.Reflection;

namespace PcBeaconAgent.Server.Tray.Services
{
    /// <summary>
    /// Single source of truth for the app name and version shown in
    /// the MainWindow header and the Settings → About section. Both
    /// <see cref="ViewModels.MainViewModel"/> and
    /// <see cref="ViewModels.SettingsViewModel"/> read from this
    /// helper so the reflection lookup lives in one place.
    /// </summary>
    /// <remarks>
    /// Static rather than an instance service because the values are
    /// process-lifetime constants — there is no per-instance state to
    /// inject, and a static helper avoids the DI ceremony for two
    /// string reads.
    /// </remarks>
    public static class AppInfo
    {
        public static string Name { get; }
        public static string Version { get; }

        static AppInfo()
        {
            Assembly asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            AssemblyName name = asm.GetName();
            Name = name.Name ?? "PcBeaconAgent.Server.Tray";
            System.Version? v = name.Version;
            Version = v != null ? $"{v.Major}.{v.Minor}.{v.Build}" : "0.0.0";
        }
    }
}
