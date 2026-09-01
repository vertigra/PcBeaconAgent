using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Core.Stores;
using PcBeaconAgent.Contracts.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Android.ViewModels;

/// <summary>
/// ViewModel for <see cref="Pages.AppsPage"/>. Shows a list of
/// launchers configured on the selected device's server, and lets
/// the user tap to launch.
/// </summary>
[QueryProperty(nameof(DeviceIp), "ip")]
public partial class AppsViewModel : ObservableObject
{
    private readonly DeviceStore mDeviceStore;
    private readonly ILogger<AppsViewModel> mLogger;

    [ObservableProperty]
    public partial string DeviceIp { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MachineName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasError { get; set; }

    public ObservableCollection<LauncherDto> Launchers { get; } = [];

    public bool HasLaunchers => Launchers.Count > 0;

    public AppsViewModel(DeviceStore deviceStore, ILogger<AppsViewModel> logger)
    {
        mDeviceStore = deviceStore;
        mLogger = logger;
        Launchers.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasLaunchers));
    }

    partial void OnDeviceIpChanged(string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        var managed = mDeviceStore.ManagedDevices.FirstOrDefault(m => m.Device.IpAddress == value);
        if (managed != null)
            MachineName = managed.Device.MachineName;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var managed = mDeviceStore.ManagedDevices.FirstOrDefault(m => m.Device.IpAddress == DeviceIp);
        if (managed == null)
        {
            StatusMessage = "Device is no longer in the managed list.";
            HasError = true;
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        HasError = false;

        try
        {
            var launchers = await managed.Launcher.GetLaunchersAsync();
            Launchers.Clear();
            foreach (var l in launchers)
                Launchers.Add(l);
        }
        catch (Exception ex)
        {
            mLogger.LogWarning(ex, "Failed to load launchers for {Ip}", DeviceIp);
            StatusMessage = "Could not load launchers. Check the connection.";
            HasError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task LaunchAsync(LauncherDto? launcher)
    {
        if (launcher == null || IsBusy) return;

        var managed = mDeviceStore.ManagedDevices.FirstOrDefault(m => m.Device.IpAddress == DeviceIp);
        if (managed == null) return;

        IsBusy = true;
        StatusMessage = $"Launching {launcher.Name}…";
        HasError = false;

        try
        {
            var response = await managed.Launcher.LaunchAsync(launcher.Id);
            if (response.Success)
            {
                StatusMessage = response.Message;
                HasError = false;
                await Task.Delay(1500);
                StatusMessage = string.Empty;
            }
            else
            {
                StatusMessage = response.Message;
                HasError = true;
            }
        }
        catch (Exception ex)
        {
            mLogger.LogWarning(ex, "Failed to launch {Id} on {Ip}", launcher.Id, DeviceIp);
            StatusMessage = "Could not launch. Check the connection.";
            HasError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
