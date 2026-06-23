using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using PcBeaconAgent.Service.JsonContext;
using PcBeaconAgent.Service.Services;

namespace PcBeaconAgent.Service.Extensions
{
    public static class SignalExtension
    {
        public static IServiceCollection AddSignal(this IServiceCollection services)
        {
            services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.TypeInfoResolverChain.Clear();
                options.SerializerOptions.TypeInfoResolverChain.Add(BeaconJsonContext.Default);
            });

            services.AddSignalR()
            .AddJsonProtocol(options =>
             {
                 options.PayloadSerializerOptions.TypeInfoResolverChain.Clear();
                 options.PayloadSerializerOptions.TypeInfoResolverChain.Add(BeaconJsonContext.Default);
             });
            return services;
        }

        public static WebApplication MapSignalHubs(this WebApplication application)
        {
            application.MapHub<BeaconHub>("/hubs/beacon");
            return application;
        }
    }
}
