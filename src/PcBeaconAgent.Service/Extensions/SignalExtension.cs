using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using PcBeaconAgent.Client.Core.Models;
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
                options.SerializerOptions.TypeInfoResolverChain.Add(AppJsonSerializerContext.Default);
            });

            services.AddSignalR()
            .AddJsonProtocol(options =>
             {
                 options.PayloadSerializerOptions.TypeInfoResolverChain.Clear();
                 options.PayloadSerializerOptions.TypeInfoResolverChain.Insert(0, BeaconJsonContext.Default);
                 options.PayloadSerializerOptions.TypeInfoResolverChain.Add(AppJsonSerializerContext.Default);
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
