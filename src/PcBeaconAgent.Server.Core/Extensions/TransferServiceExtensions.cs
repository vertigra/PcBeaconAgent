using Microsoft.Extensions.DependencyInjection;
using PcBeaconAgent.Server.Core.Services;

namespace PcBeaconAgent.Server.Core.Extensions
{
    public static class TransferServiceExtensions
    {
        /// <summary>
        /// Registers <see cref="TransferController"/> as a singleton.
        /// Intended for the server host only; client projects should not
        /// call this — the client-side equivalent is
        /// <c>TransferServiceClient</c>, created per-device by
        /// <c>DeviceFactory</c>.
        /// </summary>
        public static IServiceCollection AddTransferService(this IServiceCollection services)
        {
            services.AddSingleton<TransferController>();
            return services;
        }
    }
}
