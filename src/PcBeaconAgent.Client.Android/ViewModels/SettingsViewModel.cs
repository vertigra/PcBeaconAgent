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

    /// <summary>
    /// When true, text transfers received from the PC are automatically
    /// copied to the Android clipboard. Default: true. Stored in
    /// preferences via <see cref="StorageKeys.AutoCopyReceivedText"/>.
    /// </summary>
    [ObservableProperty]
    public partial bool AutoCopyReceivedText { get; set; }

    partial void OnAutoCopyReceivedTextChanged(bool value)
    {
        mPrefs.Set(StorageKeys.AutoCopyReceivedText, value);
    }

    public ObservableCollection<StoredKeyEntry> StoredKeys { get; } = [];

    public SettingsViewModel(IPreferencesService prefs, IDeviceStorageService deviceStorage)
    {
        mPrefs = prefs;
        mDeviceStorage = deviceStorage;
        ApiKey = mPrefs.Get(StorageKeys.ApiKey, string.Empty) ?? string.Empty;
        AutoCopyReceivedText = mPrefs.Get(StorageKeys.AutoCopyReceivedText, true);
        LoadStoredKeys();
    }

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

public sealed record StoredKeyEntry(string Identifier, string Value, string? MachineName, string? MacAddress);