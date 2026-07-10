namespace PcBeaconAgent.Server.Tray.Services
{
    /// <summary>
    /// Manages the per-user auto-start registration for the tray host.
    /// Implemented against <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>
    /// — no admin rights needed, lives in the current user's hive, and
    /// Windows honours it on every interactive logon for that user.
    /// </summary>
    /// <remarks>
    /// The Cli host is interactive-only (see
    /// <c>docs/server-cli.md</c>) — auto-start is a Tray concern.
    /// </remarks>
    public interface IAutoStartService
    {
        /// <summary>
        /// <c>true</c> if the auto-start entry currently exists in the
        /// registry. Read-only from the consumer's perspective — call
        /// <see cref="SetEnabled"/> to change it.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Adds or removes the auto-start entry. When <paramref name="enabled"/>
        /// is <c>true</c>, writes the current executable path to the
        /// <c>Run</c> key so Windows launches the tray host on the
        /// user's next logon. When <c>false</c>, deletes the entry.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the registry write/delete succeeded;
        /// <c>false</c> if the operation failed (insufficient rights,
        /// missing key, etc.). The consumer should surface a warning
        /// to the user when this returns <c>false</c>.
        /// </returns>
        bool SetEnabled(bool enabled);
    }
}
