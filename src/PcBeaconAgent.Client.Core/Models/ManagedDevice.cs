using CommunityToolkit.Mvvm.ComponentModel;
using PcBeaconAgent.Client.Core.Interfaces;

namespace PcBeaconAgent.Client.Core.Models
{
    public partial class ManagedDevice(BeaconDevice device, IAudioController audio, IMonitorController monitor) : ObservableObject
    {
        public BeaconDevice Device { get; init; } = device;

        [ObservableProperty] 
        public partial bool IsConnected { get; set; }
        public IAudioController Audio { get; init; } = audio;
        public IMonitorController Monitor { get; init; } = monitor;

        public override bool Equals(object? obj) => obj is ManagedDevice other && Device.Equals(other.Device);

        public override int GetHashCode() => Device.GetHashCode();
    }
}
