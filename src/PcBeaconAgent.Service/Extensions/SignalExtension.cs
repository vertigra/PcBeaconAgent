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
            services.ConfigureHttpJsonOptions((options =>
            {
                options.SerializerOptions.TypeInfoResolverChain.Clear();
                options.SerializerOptions.TypeInfoResolverChain.Add(ServerJsonContext.Default);
            }));

            services.AddSignalR()
            .AddJsonProtocol((options =>
             {
                 options.PayloadSerializerOptions.TypeInfoResolverChain.Clear();
                 options.PayloadSerializerOptions.TypeInfoResolverChain.Add(ServerJsonContext.Default);
             }));
            return services;
        }

        public static WebApplication MapSignalHubs(this WebApplication application)
        {
            application.MapHub<BeaconHub>("/hubs/beacon");
            return application;
        }
    }
}
