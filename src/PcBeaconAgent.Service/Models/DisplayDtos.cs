namespace PcBeaconAgent.Service.Models
{
    public record DisplayDeviceDtos(string Id, string FriendlyName, bool IsActive);
    public record DisableRequest(string Id);
}
