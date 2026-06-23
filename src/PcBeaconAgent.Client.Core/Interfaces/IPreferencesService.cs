using System.Collections.Generic;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Core.Interfaces
{
    public interface IPreferencesService
    {
        void Set<T>(string key, T value);
        T? Get<T>(string key, T defaultValue);
        void Remove(string key);
        IReadOnlyList<string> GetStoredApiKeyIdentifiers();
        Task SetSecureAsync(string key, string value);
    }
}
