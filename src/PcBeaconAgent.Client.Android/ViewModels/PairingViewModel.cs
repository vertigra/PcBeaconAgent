using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Core.Constants;
using PcBeaconAgent.Client.Core.Exceptions;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Messages;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Android.ViewModels;

[QueryProperty(nameof(ServerIp), "ip")]
[QueryProperty(nameof(ServerPort), "port")]
public partial class PairingViewModel : ObservableObject
{
    private readonly IPreferencesService mPrefs;
    private readonly IPairingServiceClient mPairingClient;

    [ObservableProperty]
    public partial string ServerIp { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int ServerPort { get; set; }

    [ObservableProperty]
    public partial string Pin { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string ErrorMessage { get; set; } = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public PairingViewModel(IPreferencesService prefs, IPairingServiceClient pairingClient)
    {
        mPrefs = prefs;
        mPairingClient = pairingClient;
    }

    [RelayCommand]
    public async Task PairAsync()
    {
        ErrorMessage = string.Empty;

        if (Pin.Length != 6 || !int.TryParse(Pin, out _))
        {
            ErrorMessage = "PIN must be exactly 6 digits.";
            return;
        }

        IsBusy = true;

        try
        {
            var result = await mPairingClient.PairAsync(ServerIp, ServerPort, Pin);

            if (result?.ApiKey is { Length: > 0 } key)
            {
                await mPrefs.SetSecureAsync(StorageKeys.ApiKeyFor(ServerIp), key);

                WeakReferenceMessenger.Default.Send(new PairingSucceededMessage(ServerIp));

                // Pop the PairingPage from the navigation stack before switching
                // tabs. Without this, GoToAsync("//MainPage") changes the active
                // tab but leaves PairingPage on top of DiscoveryPage — when the
                // user returns to the Discovery tab they see the stale pairing
                // form instead of the device list.
                await Shell.Current.GoToAsync("..");
                await Shell.Current.GoToAsync("//MainPage");
            }
            else
            {
                ErrorMessage = "Server returned an empty key. Try again.";
            }
        }
        catch (PairingHttpException ex)
        {
            ErrorMessage = ex.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Wrong PIN or pairing locked. Check the server log.",
                HttpStatusCode.Forbidden => "This PIN has already been used or expired. Tap 'Regenerate PIN' to request a new one.",
                _ => ex.Message
            };
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"Cannot reach server: {ex.Message}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            Pin = string.Empty;
        }
    }

    [RelayCommand]
    public async Task RegeneratePinAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            bool ok = await mPairingClient.RegeneratePinAsync(ServerIp, ServerPort);
            ErrorMessage = ok
                ? "New PIN generated. Check the popup next to the server's system tray."
                : "Failed to regenerate PIN.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Called automatically when the PairingPage appears. Requests a fresh
    /// PIN from the server so the 5-minute TTL window starts counting from
    /// the moment the user opened the page, not from server startup. This
    /// avoids the "PIN expired while I was reading it" failure mode and the
    /// "PIN already used" 403 after a forget-and-re-pair cycle.
    /// Silent on success — only surfaces an error if the request fails.
    /// </summary>
    [RelayCommand]
    public async Task OnAppearingAsync()
    {
        // Don't regenerate if we don't have a valid target yet.
        if (string.IsNullOrEmpty(ServerIp) || ServerPort == 0) return;

        IsBusy = true;
        try
        {
            bool ok = await mPairingClient.RegeneratePinAsync(ServerIp, ServerPort);
            if (!ok)
            {
                ErrorMessage = "Could not request a fresh PIN. Try 'Regenerate PIN' manually.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
