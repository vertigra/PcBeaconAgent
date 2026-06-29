using PcBeaconAgent.Client.Core.Models.Common;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Core.Interfaces
{
    public interface IDisplayServiceClient 
    {
        Task<IReadOnlyList<DisplayDeviceDto>> GetDisplaysAsync();
        Task DisableAsync(string id);
        Task RestoreAllAsync();
    }
}
