namespace PcBeaconAgent.Client.Core.Interfaces
{
    public interface IBeaconServerIdentity
    {
        string ApiKey { get; }
        int ApiPort { get; }
    }
}
