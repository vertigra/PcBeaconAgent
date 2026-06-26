using Android.Telephony;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Core.Constants;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Messages;
using PcBeaconAgent.Client.Core.Models.Common;
using PcBeaconAgent.Service.JsonContext;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Android.ViewModels;

[QueryProperty(nameof(ServerIp), "ip")]
[QueryProperty(nameof(ServerPort), "port")]
public partial class PairingViewModel : ObservableObject
{
    private readonly IPreferencesService mPrefs;
    private readonly IHttpClientFactory mHttpFactory;

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

    public PairingViewModel(IPreferencesService prefs, IHttpClientFactory httpFactory)
    {
        mPrefs = prefs;
        mHttpFactory = httpFactory;
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
            HttpClient client = mHttpFactory.CreateClient();
            string url = $"http://{ServerIp}:{ServerPort}/api/pair";
            HttpResponseMessage response = await client.PostAsJsonAsync(url, new PairRequestDto(Pin), ServerJsonContext.Default.PairRequestDto);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync(ServerJsonContext.Default.PairResponseDto);

                if (result?.ApiKey is { Length: > 0 } key)
                {
                    await mPrefs.SetSecureAsync(StorageKeys.ApiKeyFor(ServerIp), key);

                    WeakReferenceMessenger.Default.Send(new PairingSucceededMessage(ServerIp));

                    await Shell.Current.GoToAsync("//MainPage");
                }
                else
                {
                    ErrorMessage = "Server returned an empty key. Try again.";
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                ErrorMessage = "Wrong PIN or pairing locked. Check the server log.";
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                ErrorMessage = "Pairing mode is inactive. PIN may have expired — regenerate on the server.";
            }
            else
            {
                ErrorMessage = $"Unexpected server error ({(int)response.StatusCode}).";
            }
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
            var client = mHttpFactory.CreateClient();
            var url = $"http://{ServerIp}:{ServerPort}/api/pair/regenerate";
            var response = await client.PostAsync(url, null);

            ErrorMessage = response.IsSuccessStatusCode
                ? "New PIN generated. Check the server console."
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
}