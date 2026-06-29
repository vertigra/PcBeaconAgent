namespace PcBeaconAgent.Server.Core.Interfaces
{
    public interface IBeaconServerIdentity
    {
        string ApiKey { get; }
        int ApiPort { get; }
    }
}
