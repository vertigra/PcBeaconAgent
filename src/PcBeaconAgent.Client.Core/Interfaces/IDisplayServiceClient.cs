using PcBeaconAgent.Contracts.Models;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Core.Interfaces
{
    public interface IDisplayServiceClient
    {
        Task<DisplayListResponseDto> GetDisplaysAsync();
        Task DisableAsync(string id);
        Task RestoreAllAsync();
    }
}
