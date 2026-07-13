using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using PcBeaconAgent.Contracts;
using PcBeaconAgent.Server.Core.Services;

namespace PcBeaconAgent.Server.Core.Extensions
{
    public static class BeaconServiceHubExtensions
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

            // ConnectionTracker is a singleton so it survives across
            // hub instances (the hub is transient, created per
            // connection). Constructed on the UI thread in the tray
            // host so it captures the WPF Dispatcher sync context.
            services.AddSingleton<IConnectionTracker, ConnectionTracker>();

            return services;
        }

        public static WebApplication MapSignalHubs(this WebApplication application)
        {
            application.MapHub<BeaconServiceHub>("/hubs/beacon");
            return application;
        }
    }
}
