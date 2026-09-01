using CommunityToolkit.Mvvm.ComponentModel;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Contracts.Models;

namespace PcBeaconAgent.Client.Core.Models
{
    public partial class ManagedDevice(BeaconDevice device, IAudioServiceClient audio, IDisplayServiceClient monitor, ITransferServiceClient transfer, ILauncherServiceClient launcher) : ObservableObject
    {
        public BeaconDevice Device { get; init; } = device;

        [ObservableProperty] 
        public partial bool IsOnline { get; set; }
        public IAudioServiceClient Audio { get; init; } = audio;
        public IDisplayServiceClient Display { get; init; } = monitor;
        public ITransferServiceClient Transfer { get; init; } = transfer;
        public ILauncherServiceClient Launcher { get; init; } = launcher;
        public override bool Equals(object? obj) => obj is ManagedDevice other && Device.Equals(other.Device);
        public override int GetHashCode() => Device.GetHashCode();
    }
}
