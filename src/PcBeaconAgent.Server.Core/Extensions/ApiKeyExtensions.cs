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
