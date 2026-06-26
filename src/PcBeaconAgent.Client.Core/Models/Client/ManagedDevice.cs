using CommunityToolkit.Mvvm.ComponentModel;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models.Common;

namespace PcBeaconAgent.Client.Core.Models.Client
{
    public partial class ManagedDevice(BeaconDevice device, IAudioController audio, IDisplayController monitor) : ObservableObject
    {
        public BeaconDevice Device { get; init; } = device;

        [ObservableProperty] 
        public partial bool IsOnline { get; set; }
        public IAudioController Audio { get; init; } = audio;
        public IDisplayController Monitor { get; init; } = monitor;
        public override bool Equals(object? obj) => obj is ManagedDevice other && Device.Equals(other.Device);
        public override int GetHashCode() => Device.GetHashCode();
    }
}
