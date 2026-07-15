using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PcBeaconAgent.Server.Core.Configuration;
using System;
using System.Security.Cryptography;
using System.Text;

namespace PcBeaconAgent.Server.Core.Extensions
{
    public static class ApiKeyExtensions
    {
        public static RouteGroupBuilder RequireApiKey(this RouteGroupBuilder group, AppSettings settings)
        {
            group.AddEndpointFilter(async (context, next) =>
            {
                // Fail-closed: empty or whitespace ApiKey means no key is
                // configured — reject all requests. The server generates a
                // key on first run (server.key), so this only triggers if
                // the key file is missing or corrupted.
                if (string.IsNullOrWhiteSpace(settings.Server.ApiKey))
                {
                    return Results.Unauthorized();
                }

                string? provided = context.HttpContext.Request.Headers["X-Api-Key"];

                // Query-string fallback for REST clients (Android app
                // sends the key via ?api_key= query parameter). This is
                // acceptable for REST endpoints over LAN — query strings
                // are only logged by proxies if one is in front, which is
                // not the case for a direct LAN connection.
                if (string.IsNullOrEmpty(provided))
                {
                    provided = context.HttpContext.Request.Query["api_key"];
                }

                if (string.IsNullOrEmpty(provided))
                {
                    return Results.Unauthorized();
                }

                // Constant-time comparison to prevent timing attacks.
                if (!FixedTimeEquals(provided, settings.Server.ApiKey))
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
