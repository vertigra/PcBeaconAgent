namespace PcBeaconAgent.Client.Core.Interfaces
{
    public interface IPreferencesService
    {
        void Set<T>(string key, T value);
        T? Get<T>(string key, T defaultValue);
    }
}
