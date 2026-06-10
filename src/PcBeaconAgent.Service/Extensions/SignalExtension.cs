using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using PcBeaconAgent.Service.Services;

namespace PcBeaconAgent.Service.Extensions
{
    public static class SignalExtension
    {
        public static IServiceCollection AddSignal(this IServiceCollection services)
        {
            services.AddSignalR();
            return services;
        }

        public static WebApplication MapSignalHubs(this WebApplication application)
        {
            application.MapHub<BeaconHub>("/beaconHub");
            return application;
        }
    }
}
