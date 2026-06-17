using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PcBeaconAgent.Service.Configuration;
using System;


namespace PcBeaconAgent.Service.Extensions
{
    // FIX (новый файл): лёгкая проверка общего секрета для REST-эндпоинтов, без
    // подключения полноценного pipeline аутентификации/авторизации ASP.NET Core —
    // тут достаточно сравнения одного заголовка.
    public static class ApiKeyExtensions
    {
        public static RouteGroupBuilder RequireApiKey(this RouteGroupBuilder group, AppSettings settings)
        {
            group.AddEndpointFilter(async (context, next) =>
            {
                if (string.IsNullOrEmpty(settings.Server.ApiKey))
                {
                    return await next(context);
                }

                string? provided = context.HttpContext.Request.Headers["X-Api-Key"];
                if (!string.Equals(provided, settings.Server.ApiKey, StringComparison.Ordinal))
                {
                    return Results.Unauthorized();
                }

                return await next(context);
            });

            return group;
        }
    }
}
