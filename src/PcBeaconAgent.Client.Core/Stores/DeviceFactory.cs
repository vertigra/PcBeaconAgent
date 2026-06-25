using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models;
using PcBeaconAgent.Client.Core.Models.Common;
using PcBeaconAgent.Client.Core.Services;
using System.Net.Http;

namespace PcBeaconAgent.Client.Core.Stores
{
    // FIX: добавлена зависимость IPreferencesService — пробрасывается в
    // AudioController для динамического чтения ключа на каждый запрос (см. выше).
    // DI-контейнер разрешит её автоматически, т.к. IPreferencesService уже
    // зарегистрирован в MauiProgram.cs — менять регистрацию самого DeviceFactory не нужно.
    public class DeviceFactory(IHttpClientFactory mHttpClientFactory, IPreferencesService mPrefs)
    {
        public ManagedDevice Create(BeaconDevice beacon)
        {
            return new ManagedDevice(
                beacon,
                new AudioController(beacon.IpAddress, beacon.ApiPort, mPrefs, mHttpClientFactory.CreateClient()),
                new MonitorController(beacon.IpAddress, beacon.ApiPort, mHttpClientFactory.CreateClient())
            );
        }
    }
}