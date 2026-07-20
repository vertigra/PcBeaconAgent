namespace PcBeaconAgent.Server.Tray.Models
{
    /// <summary>
    /// Severity for transient notifications shown via
    /// <c>INotificationService.ShowTransient</c>. Drives the accent
    /// stripe colour on <c>TransientToastWindow</c> via a XAML
    /// <c>DataTrigger</c>.
    /// </summary>
    public enum NotificationSeverity
    {
        Info,
        Warning,
        Error
    }
}
