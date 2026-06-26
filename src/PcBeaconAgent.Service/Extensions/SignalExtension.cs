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
            services.ConfigureHttpJsonOptions((System.Action<Microsoft.AspNetCore.Http.Json.JsonOptions>)(options =>
            {
                options.SerializerOptions.TypeInfoResolverChain.Clear();
                options.SerializerOptions.TypeInfoResolverChain.Add((System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver)ProjectJsonContext.Default);
            }));

            services.AddSignalR()
            .AddJsonProtocol((System.Action<Microsoft.AspNetCore.SignalR.JsonHubProtocolOptions>)(options =>
             {
                 options.PayloadSerializerOptions.TypeInfoResolverChain.Clear();
                 options.PayloadSerializerOptions.TypeInfoResolverChain.Add((System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver)ProjectJsonContext.Default);
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
