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

        // FIX (новый метод): аналог Set для "api_key*"-ключей, но дожидается
        // реальной записи в SecureStorage перед возвратом, вместо fire-and-forget.
        // Нужен там, где дальнейший код полагается на немедленную видимость
        // записанного значения — конкретно: PairingViewModel должен дождаться
        // этого ДО уведомления через WeakReferenceMessenger, иначе обработчик
        // в MainViewModel может прочитать ключ раньше, чем он реально появится.
        Task SetSecureAsync(string key, string value);
    }
}
