using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcBeaconAgent.Client.Core.Constants;
using PcBeaconAgent.Client.Core.Interfaces;

namespace PcBeaconAgent.Client.Android.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IPreferencesService mPrefs;

    [ObservableProperty]
    public partial string ApiKey { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    public SettingsViewModel(IPreferencesService prefs)
    {
        mPrefs = prefs;
        ApiKey = mPrefs.Get(StorageKeys.ApiKey, string.Empty) ?? string.Empty;
    }

    [RelayCommand]
    public async System.Threading.Tasks.Task SaveAsync()
    {
        mPrefs.Set(StorageKeys.ApiKey, ApiKey.Trim());
        StatusMessage = "Key saved.";
        await System.Threading.Tasks.Task.Delay(2000);
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    public async System.Threading.Tasks.Task ClearAsync()
    {
        ApiKey = string.Empty;
        mPrefs.Set(StorageKeys.ApiKey, string.Empty);
        StatusMessage = "Key cleared.";
        await System.Threading.Tasks.Task.Delay(2000);
        StatusMessage = string.Empty;
    }
}