using System.Threading.RateLimiting;
using BazingaGame.Models;
using BazingaGame.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;

namespace BazingaGame.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddGameServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString("Redis");

        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            // abortConnect=false lets the app start even if Redis is temporarily unreachable;
            // the multiplexer reconnects automatically rather than throwing at startup.
            var configOptions = ConfigurationOptions.Parse(redisConnectionString);
            configOptions.AbortOnConnectFail = false;

            var multiplexer = ConnectionMultiplexer.Connect(configOptions);
            services.AddSingleton<IConnectionMultiplexer>(multiplexer);
            services.AddSingleton<IGameService, RedisGameService>();
        }
        else
        {
            services.AddMemoryCache();
            services.AddSingleton<IGameService, GameService>();
        }

        return services;
    }

    internal static IServiceCollection AddGameSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            // Swashbuckle cannot introspect Dictionary<string,string[]> record properties —
            // it throws a NullReferenceException during schema generation. Provide the full
            // ValidationErrorResponse schema manually so Swagger shows the real 400 shape.
            options.MapType<ValidationErrorResponse>(() => new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["title"] = new OpenApiSchema { Type = "string" },
                    ["status"] = new OpenApiSchema { Type = "integer" },
                    ["errors"] = new OpenApiSchema
                    {
                        Type = "object",
                        Example = new OpenApiObject
                        {
                            ["Player"] = new OpenApiArray
                            {
                                new OpenApiString("The field Player must be between 1 and 5.")
                            }
                        }
                    }
                },
                Example = new OpenApiObject
                {
                    ["title"] = new OpenApiString("One or more validation errors occurred."),
                    ["status"] = new OpenApiInteger(400),
                    ["errors"] = new OpenApiObject
                    {
                        ["Player"] = new OpenApiArray
                        {
                            new OpenApiString("The field Player must be between 1 and 5.")
                        }
                    }
                }
            });
        });

        return services;
    }

    internal static IServiceCollection AddGameRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.OnRejected = async (context, _) =>
            {
                context.HttpContext.Response.StatusCode = 429;
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsync(
                    "{\"error\":\"Too many requests. Please slow down.\"}");
            };

            // Per-IP fixed window: each client IP gets its own independent counter
            options.AddPolicy("play", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            options.AddPolicy("read", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));
        });

        return services;
    }

    internal static IHealthChecksBuilder AddGameHealthChecks(
        this IServiceCollection services, IConfiguration configuration)
    {
        var healthChecks = services.AddHealthChecks()
            .AddUrlGroup(
                new Uri("https://codechallenge.boohma.com/random"),
                name: "random-api",
                tags: ["ready"]);

        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnectionString))
            healthChecks.AddRedis(redisConnectionString, name: "redis", tags: ["ready"]);

        return healthChecks;
    }
}
