using CommunityToolkit.Mvvm.ComponentModel;

namespace PcBeaconAgent.Server.Tray.ViewModels
{
    /// <summary>
    /// View model for the Files tab. Currently a placeholder — the
    /// cross-device file transfer feature is tracked in the roadmap
    /// (Tier 3 "Cross-device clipboard &amp; file transfer"). When it
    /// ships, this VM will own the incoming-files list, the default
    /// save folder, and the auto-accept toggle.
    /// </summary>
    public partial class FilesViewModel : ObservableObject
    {
        // No state yet. The FilesView shows a static "feature coming
        // soon" placeholder. Keeping the VM around (instead of just
        // putting the placeholder text in XAML) so the wiring is in
        // place — when transfer lands, this VM gets observables and
        // commands without having to touch MainViewModel or the
        // TabControl structure.
    }
}
