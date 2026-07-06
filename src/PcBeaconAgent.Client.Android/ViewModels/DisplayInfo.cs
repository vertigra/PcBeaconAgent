using CommunityToolkit.Mvvm.ComponentModel;

namespace PcBeaconAgent.Client.Android.ViewModels;

/// <summary>
/// A UI-facing representation of a display device, used as an item in the
/// <see cref="DisplayControlViewModel.Displays"/> collection. Wraps the
/// transport DTO (<c>DisplayDeviceDto</c>) with observable properties so
/// the MAUI bindings update when the server state changes.
/// </summary>
public partial class DisplayInfo(string id, string friendlyName) : ObservableObject
{
    public string Id { get; } = id;
    public string FriendlyName { get; } = friendlyName;

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    /// <summary>
    /// True when this display is the GDI primary (desktop position 0,0).
    /// Shown as a badge in the UI so the user understands which display
    /// is the main one. Disabling the primary promotes another display
    /// to primary automatically (server-side fix in DisplayController).
    /// </summary>
    [ObservableProperty]
    public partial bool IsPrimary { get; set; }

    /// <summary>
    /// True when this is the only active display remaining. The UI uses
    /// this to disable the Disable button — Windows rejects disabling the
    /// last active display, and the server now returns a clear error for
    /// it, but blocking the button up front avoids a round-trip and a
    /// confusing error toast.
    /// </summary>
    [ObservableProperty]
    public partial bool IsLastActive { get; set; }
}
