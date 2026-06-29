namespace PcBeaconAgent.Client.Core.Models.Common
{
    public record DisplayDeviceDto(string Id, string FriendlyName, bool IsActive);
    public record DisableRequestDto(string Id);
}
