using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using PcBeaconAgent.Client.Core.Models;
using PcBeaconAgent.Service.Services;

namespace PcBeaconAgent.Service.Extensions
{
    public static class SignalExtension
    {
        public static IServiceCollection AddSignal(this IServiceCollection services)
        {
            services.AddSignalR()
            .AddJsonProtocol(options =>
             {
                 options.PayloadSerializerOptions.TypeInfoResolverChain.Insert(0, BeaconJsonContext.Default);
             });
            return services;
        }

        public static WebApplication MapSignalHubs(this WebApplication application)
        {
            application.MapHub<BeaconHub>("/beaconHub");
            return application;
        }
    }
}
