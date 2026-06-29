using Microsoft.Extensions.DependencyInjection;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Services;

namespace PcBeaconAgent.Client.Core.Extensions
{
    public static class BeaconServerIdentityExtensions
    {
        /// <summary>
        /// Registers <see cref="IBeaconServerIdentity"/> with its internal
        /// <see cref="BeaconServerIdentity"/> implementation. Intended for
        /// the server host only; client projects should not call this.
        /// </summary>
        public static IServiceCollection AddBeaconServerIdentity(this IServiceCollection services)
        {
            services.AddSingleton<IBeaconServerIdentity, BeaconServerIdentity>();
            return services;
        }
    }
}