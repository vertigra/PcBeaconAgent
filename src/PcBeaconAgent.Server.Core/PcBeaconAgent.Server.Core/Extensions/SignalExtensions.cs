using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using PcBeaconAgent.Client.Core.JsonContext;
using PcBeaconAgent.Server.Core.Services;

namespace PcBeaconAgent.Server.Core.Extensions
{
    public static class SignalExtensions
    {
        public static IServiceCollection AddSignal(this IServiceCollection services)
        {
            services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.TypeInfoResolverChain.Clear();
                options.SerializerOptions.TypeInfoResolverChain.Add(ProjectJsonContext.Default);
            });

            services.AddSignalR().AddJsonProtocol(options =>
            {
                 options.PayloadSerializerOptions.TypeInfoResolverChain.Clear();
                 options.PayloadSerializerOptions.TypeInfoResolverChain.Add(ProjectJsonContext.Default);
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
