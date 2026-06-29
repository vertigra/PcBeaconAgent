namespace PcBeaconAgent.Service.Interfaces
{
    public interface IBeaconServerIdentity
    {
        string ApiKey { get; }
        int ApiPort { get; }
    }
}
