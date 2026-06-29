using Microsoft.Extensions.DependencyInjection;
using PcBeaconAgent.Server.Core.Interfaces;
using PcBeaconAgent.Server.Core.Services;

namespace PcBeaconAgent.Server.Core.Extensions
{
    public static class PairingServiceExtensions
    {
        /// <summary>
        /// Registers <see cref="IPairingService"/> with its internal
        /// <see cref="PairingService"/> implementation. Intended for the
        /// server host only; client projects should not call this.
        /// </summary>
        public static IServiceCollection AddPairingService(this IServiceCollection services)
        {
            services.AddSingleton<IPairingService, PairingService>();
            return services;
        }
    }
}