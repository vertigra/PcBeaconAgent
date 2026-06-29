using PcBeaconAgent.Client.Core.Models.Common;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Core.Interfaces
{
    public interface IAudioServiceClient
    {
        Task<IReadOnlyList<AudioDeviceDto>> GetDevicesAsync();
        Task<string?> GetDefaultDeviceIdAsync();
        Task SetDefaultAsync(string id);
    }
}
