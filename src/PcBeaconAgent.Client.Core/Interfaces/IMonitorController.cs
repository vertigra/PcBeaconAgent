using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Core.Interfaces
{
    public interface IMonitorController 
    { 
        Task TogglePowerAsync(bool on); 
    }
}
