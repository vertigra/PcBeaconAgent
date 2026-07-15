using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PcBeaconAgent.Server.Core.Interfaces;
using System;
using System.Security.Cryptography;
using System.Text;

namespace PcBeaconAgent.Server.Core.Extensions
{
    public static class ApiKeyExtensions
    {
        /// <summary>
        /// Requires an API key on all endpoints in the group. The key
        /// is resolved from <see cref="IBeaconServerIdentity.ApiKey"/>
        /// (loaded from appsettings.json or server.key), NOT from
        /// AppSettings.Server.ApiKey — the latter is empty when the key
        /// is auto-generated.
        /// </summary>
        public static RouteGroupBuilder RequireApiKey(this RouteGroupBuilder group, IBeaconServerIdentity identity)
        {
            group.AddEndpointFilter(async (context, next) =>
            {
                if (string.IsNullOrWhiteSpace(identity.ApiKey))
                {
                    return Results.Unauthorized();
                }

                string? provided = context.HttpContext.Request.Headers["X-Api-Key"];

                // Query-string fallback for REST clients (Android app
                // sends the key via ?api_key= query parameter).
                if (string.IsNullOrEmpty(provided))
                {
                    provided = context.HttpContext.Request.Query["api_key"];
                }

                if (string.IsNullOrEmpty(provided))
                {
                    return Results.Unauthorized();
                }

                if (!FixedTimeEquals(provided, identity.ApiKey))
                {
                    return Results.Unauthorized();
                }

                return await next(context);
            });

            return group;
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            byte[] aBytes = Encoding.UTF8.GetBytes(a);
            byte[] bBytes = Encoding.UTF8.GetBytes(b);
            return aBytes.Length == bBytes.Length &&
                   CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
        }
    }
}
