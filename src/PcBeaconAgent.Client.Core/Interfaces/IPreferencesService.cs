using System.Collections.Generic;

namespace PcBeaconAgent.Client.Core.Interfaces
{
    public interface IPreferencesService
    {
        void Set<T>(string key, T value);
        T? Get<T>(string key, T defaultValue);
        void Remove(string key);

        // FIX (новый метод): SecureStorage не даёт перечислить хранящиеся в нём
        // ключи — единственный способ узнать "что там есть" — вести отдельный
        // индекс самостоятельно. Возвращает идентификаторы (IP-адреса серверов,
        // либо строка "global" для ключа, введённого вручную) для которых СЕЙЧАС
        // есть сохранённый ключ.
        IReadOnlyList<string> GetStoredApiKeyIdentifiers();
    }
}
