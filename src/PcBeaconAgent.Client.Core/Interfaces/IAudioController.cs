
using PcBeaconAgent.Client.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Core.Interfaces
{
    public interface IAudioController
    {
        // Новые методы — нужны для экрана управления, раньше интерфейс умел
        // только переключать дефолтное устройство "вслепую", без возможности
        // показать пользователю список и текущее состояние.
        Task<IReadOnlyList<AudioDeviceInfo>> GetDevicesAsync();
        Task<string?> GetDefaultDeviceIdAsync();
        Task SetDefaultAsync(string id);
    }
}
