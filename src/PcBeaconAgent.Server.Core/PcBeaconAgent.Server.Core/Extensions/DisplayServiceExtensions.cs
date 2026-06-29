using Microsoft.Extensions.DependencyInjection;
using PcBeaconAgent.Client.Core.Services;
using PcBeaconAgent.Server.Core.Services;

namespace PcBeaconAgent.Client.Core.Extensions
{
    public static class DisplayServiceExtensions
    {
        /// <summary>
        /// Registers <see cref="DisplayController"/> as a singleton. Intended
        /// for the server host only; client projects should not call this —
        /// the client-side equivalent is <see cref="DisplayServiceClient"/>,
        /// which is created per-device by DeviceFactory.
        /// </summary>
        public static IServiceCollection AddDisplayService(this IServiceCollection services)
        {
            services.AddSingleton<DisplayController>();
            return services;
        }
    }
}