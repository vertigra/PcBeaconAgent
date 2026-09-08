using Microsoft.Extensions.DependencyInjection;
using PcBeaconAgent.Server.Core.Services;

namespace PcBeaconAgent.Server.Core.Extensions
{
    public static class LauncherServiceExtensions
    {
        public static IServiceCollection AddLauncherService(this IServiceCollection services)
        {
            services.AddSingleton<LauncherController>();
            return services;
        }
    }
}
