namespace PcBeaconAgent.Service.Models
{
    public record DisplayDeviceDto(string Id, string FriendlyName, bool IsActive);
    public record DisableRequestDto(string Id);
}
