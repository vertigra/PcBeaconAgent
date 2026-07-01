using System.Collections.Generic;

namespace PcBeaconAgent.Contracts.Models
{
    public record DisplayDeviceDto(string Id, string FriendlyName, bool IsActive, bool IsPrimary);
    public record DisableRequestDto(string Id);

    /// <summary>
    /// Response payload for GET /api/display/list. Wraps the device list
    /// together with the current display topology (Extend / Clone / etc.)
    /// so the client does not have to infer it from the device list.
    /// </summary>
    public record DisplayListResponseDto(List<DisplayDeviceDto> Displays, string Topology);
}
