using Android.Telephony;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Controls;
using PcBeaconAgent.Client.Core.Constants;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Messages;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Android.ViewModels;

/// <summary>
/// Handles PIN-based pairing with a discovered server.
/// Receives the server IP and port via Shell query parameters,
/// submits the PIN to POST /api/pair, and stores the returned ApiKey.
/// </summary>
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

    // FIX: добавлен [NotifyPropertyChangedFor(nameof(HasError))] — при изменении
    // ErrorMessage автоматически уведомляется и HasError, на который теперь
    // подписан IsVisible в XAML вместо невалидного x:Static-конвертера.
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
            var client = mHttpFactory.CreateClient();
            var url = $"http://{ServerIp}:{ServerPort}/api/pair";

            var response = await client.PostAsJsonAsync(url, new { Pin });

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<PairResponse>();

                if (result?.ApiKey is { Length: > 0 } key)
                {
                    // FIX: было mPrefs.Set(...) — fire-and-forget запись. Send() ниже синхронно
                    // вызывает обработчик в MainViewModel, который сразу пытается читать этот
                    // же ключ через Remember() — реальная запись в SecureStorage к этому
                    // моменту почти никогда не успевала завершиться, авто-ретрай получал
                    // NotPairedException и тихо уходил на PairingPage. Устройство оставалось
                    // висеть в DiscoveredDevices до следующего ручного нажатия Remember, когда
                    // запись уже успевала закончиться. Теперь запись дожидается явно, и только
                    // после этого отправляется уведомление — гонка устранена детерминированно,
                    // а не "обычно успевает".
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

    private sealed record PairResponse(string ApiKey);
}