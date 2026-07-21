namespace PcBeaconAgent.Client.Android.ViewModels
{
    /// <summary>
    /// Parsed topology classification used by
    /// <see cref="DisplayControlViewModel.TopologyKind"/>. Mirrors the
    /// Windows CCD <c>DisplayConfigTopologyId</c> enum values that the
    /// server reports via <c>DisplayListResponseDto.Topology</c>, plus
    /// <see cref="None"/> for the "no topology reported yet" case
    /// (server just booted, query failed, or the topology string did
    /// not match any of the expected names).
    /// </summary>
    public enum DisplayTopologyKind
    {
        /// <summary>
        /// Topology not yet reported or unrecognised. The icon row is
        /// hidden by the surrounding <c>IsVisible</c> binding on
        /// <c>HasTopology</c>, so this value is mostly a safe default.
        /// </summary>
        None,

        /// <summary>
        /// Desktop extends across all active displays (the typical
        /// multi-monitor setup). Each display shows a different portion
        /// of the desktop.
        /// </summary>
        Extend,

        /// <summary>
        /// All active displays mirror the same desktop content. Common
        /// for "presenter mode" with a projector.
        /// </summary>
        Clone,

        /// <summary>
        /// Only the internal panel (laptop screen) is active; external
        /// outputs are dark.
        /// </summary>
        Internal,

        /// <summary>
        /// Only an external display is active; the internal panel is
        /// dark (laptop lid closed or manually disabled).
        /// </summary>
        External
    }
}
