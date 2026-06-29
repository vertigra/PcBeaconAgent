using Microsoft.Extensions.DependencyInjection;
using PcBeaconAgent.Client.Core.Services;
using PcBeaconAgent.Server.Core.Services;

namespace PcBeaconAgent.Server.Core.Extensions
{
    public static class AudioServiceExtensions
    {
        /// <summary>
        /// Registers <see cref="AudioController"/> as a singleton. Intended
        /// for the server host only; client projects should not call this —
        /// the client-side equivalent is <see cref="AudioServiceClient"/>,
        /// which is created per-device by DeviceFactory.
        /// </summary>
        public static IServiceCollection AddAudioService(this IServiceCollection services)
        {
            services.AddSingleton<AudioController>();
            return services;
        }
    }
}