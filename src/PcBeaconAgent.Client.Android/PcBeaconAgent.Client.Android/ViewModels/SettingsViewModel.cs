using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcBeaconAgent.Client.Core.Constants;
using PcBeaconAgent.Client.Core.Interfaces;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Android.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IPreferencesService mPrefs;
    private readonly IDeviceStorageService mDeviceStorage;

    [ObservableProperty]
    public partial string ApiKey { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    public ObservableCollection<StoredKeyEntry> StoredKeys { get; } = [];

    // FIX: добавлена зависимость IDeviceStorageService — нужна, чтобы по IP
    // (которым проиндексирован ключ) найти запомненное устройство и подтянуть
    // его MachineName/MacAddress. Сервис уже зарегистрирован как Singleton в
    // MauiProgram.cs — добавление параметра в конструктор ничего больше не требует.
    public SettingsViewModel(IPreferencesService prefs, IDeviceStorageService deviceStorage)
    {
        mPrefs = prefs;
        mDeviceStorage = deviceStorage;
        ApiKey = mPrefs.Get(StorageKeys.ApiKey, string.Empty) ?? string.Empty;
        LoadStoredKeys();
    }

    // FIX (новый публичный метод): тонкая обёртка над приватным LoadStoredKeys —
    // вызывается из SettingsPage.OnAppearing при каждом показе страницы, а не
    // только из конструктора.
    public void RefreshStoredKeys() => LoadStoredKeys();

    private void LoadStoredKeys()
    {
        StoredKeys.Clear();

        var knownDevices = mDeviceStorage.LoadDevices().ToList();

        foreach (var identifier in mPrefs.GetStoredApiKeyIdentifiers())
        {
            string storageKey = identifier == "global"
                ? StorageKeys.ApiKey
                : StorageKeys.ApiKeyFor(identifier);

            string value = mPrefs.Get(storageKey, string.Empty) ?? string.Empty;

            var matchedDevice = identifier == "global"
                ? null
                : knownDevices.FirstOrDefault(d => d.IpAddress == identifier);

            StoredKeys.Add(new StoredKeyEntry(
                Identifier: identifier,
                Value: value,
                MachineName: matchedDevice?.MachineName,
                MacAddress: matchedDevice?.MacAddress));
        }
    }

    [RelayCommand]
    public void RemoveStoredKey(StoredKeyEntry entry)
    {
        string storageKey = entry.Identifier == "global"
            ? StorageKeys.ApiKey
            : StorageKeys.ApiKeyFor(entry.Identifier);

        mPrefs.Remove(storageKey);
        StoredKeys.Remove(entry);

        if (entry.Identifier == "global")
            ApiKey = string.Empty;
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        mPrefs.Set(StorageKeys.ApiKey, ApiKey.Trim());
        StatusMessage = "Key saved.";
        LoadStoredKeys();

        await Task.Delay(2000);
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    public async Task ClearAsync()
    {
        ApiKey = string.Empty;
        mPrefs.Remove(StorageKeys.ApiKey);
        StatusMessage = "Key cleared.";
        LoadStoredKeys();

        await Task.Delay(2000);
        StatusMessage = string.Empty;
    }
}

// FIX: добавлены MachineName и MacAddress — оба nullable, так как для "global"
// записи (ручной fallback-ключ без привязки к конкретному устройству) их
// просто нет, а для per-device записи устройство теоретически могло быть
// "забыто" (Forget) после того как ключ для него уже был сохранён где-то ещё
// (например, вручную через SettingsPage под тем же IP) — в этом случае
// совпадения в knownDevices не найдётся, и оба поля останутся null.
public sealed record StoredKeyEntry(string Identifier, string Value, string? MachineName, string? MacAddress);