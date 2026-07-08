namespace PcBeaconAgent.Server.Tray.Services
{
    /// <summary>
    /// Severity for transient notifications shown via
    /// <see cref="INotificationService.ShowTransient"/>.
    /// Maps 1:1 to Hardcodet's <c>BalloonIcon</c> for the current
    /// balloon-based implementation; when the Tier 3 "custom balloon
    /// positioning" work lands and we replace balloons with our own WPF
    /// popups, this enum will stay and the BalloonIcon mapping disappears.
    /// </summary>
    public enum NotificationSeverity
    {
        Info,
        Warning,
        Error
    }
}
