using PcBeaconAgent.Service.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Core.Interfaces
{
    public interface IDisplayController 
    {
        Task<IReadOnlyList<DisplayDeviceDto>> GetDisplaysAsync();
        Task DisableAsync(string id);
        Task RestoreAllAsync();
    }
}
