using CommunityToolkit.Mvvm.ComponentModel;
using PcBeaconAgent.Contracts.Models;

namespace PcBeaconAgent.Client.Core.Models
{
    /// <summary>
    /// Observable wrapper around <see cref="BeaconDevice"/> for UI data binding.
    /// Exposes the same properties as <see cref="BeaconDevice"/> but raises
    /// PropertyChanged notifications via [ObservableProperty].
    /// </summary>
    public partial class BeaconDeviceViewModel : ObservableObject
    {
        private readonly BeaconDevice mSource;

        public BeaconDeviceViewModel(BeaconDevice source)
        {
            mSource = source;
        }

        /// <summary>The underlying POCO model — use for serialization and transport.</summary>
        public BeaconDevice Source => mSource;

        [ObservableProperty]
        public partial string MachineName { get; set; }

        [ObservableProperty]
        public partial string IpAddress { get; set; }

        [ObservableProperty]
        public partial int ApiPort { get; set; }

        [ObservableProperty]
        public partial string MacAddress { get; set; }

        [ObservableProperty]
        public partial string InterfaceName { get; set; }

        [ObservableProperty]
        public partial string InterfaceType { get; set; }

        /// <summary>
        /// Synchronizes the [ObservableProperty] values from the underlying
        /// <see cref="mSource"/>. Call this after the source is updated
        /// (e.g. after RefreshDeviceDetailsAsync populates new data).
        /// </summary>
        public void RefreshFromSource()
        {
            MachineName = mSource.MachineName;
            IpAddress = mSource.IpAddress;
            ApiPort = mSource.ApiPort;
            MacAddress = mSource.MacAddress;
            InterfaceName = mSource.InterfaceName;
            InterfaceType = mSource.InterfaceType;
        }

        /// <summary>
        /// Synchronizes the underlying <see cref="mSource"/> from the
        /// [ObservableProperty] values. Call this before serializing the
        /// device to JSON or sending it over the wire.
        /// </summary>
        public void PushToSource()
        {
            mSource.MachineName = MachineName;
            mSource.IpAddress = IpAddress;
            mSource.ApiPort = ApiPort;
            mSource.MacAddress = MacAddress;
            mSource.InterfaceName = InterfaceName;
            mSource.InterfaceType = InterfaceType;
        }
    }
}