namespace PcBeaconAgent.Client.Core.Interfaces
{
    public interface IPreferencesService
    {
        void Set<T>(string key, T value);
        T? Get<T>(string key, T defaultValue);

        // FIX: новый метод — без него нет способа физически удалить ключ
        // при "Forget", только перезаписать его пустым значением, что не то же самое.
        void Remove(string key);
    }
}
