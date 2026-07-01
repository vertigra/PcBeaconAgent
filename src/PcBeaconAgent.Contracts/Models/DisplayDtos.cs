namespace PcBeaconAgent.Contracts.Models
{
    public record DisplayDeviceDto(string Id, string FriendlyName, bool IsActive, bool IsPrimary);
    public record DisableRequestDto(string Id);
}
