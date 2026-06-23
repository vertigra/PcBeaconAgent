namespace PcBeaconAgent.Service.Interfaces
{
    public interface IBeaconAnnouncementService
    {
        string ApiKey { get; }
        int ApiPort { get; }
    }
}
