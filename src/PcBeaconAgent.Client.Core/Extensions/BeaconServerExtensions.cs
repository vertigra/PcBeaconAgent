using Microsoft.Extensions.DependencyInjection;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Services;

namespace PcBeaconAgent.Client.Core.Extensions
{
    public static class BeaconServerExtensions
    {
        /// <summary>
        /// Registers <see cref="IBeaconServer"/> with its internal
        /// <see cref="BeaconServer"/> implementation. Intended for the
        /// server host only; client projects should not call this.
        /// </summary>
        public static IServiceCollection AddBeaconServer(this IServiceCollection services)
        {
            services.AddSingleton<IBeaconServer, BeaconServer>();
            return services;
        }
    }
}