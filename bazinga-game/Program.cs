using System.Threading.RateLimiting;
using BazingaGame.Middleware;
using BazingaGame.Services;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration)
          .WriteTo.Console(outputTemplate:
              "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);

// Suppress the default 400 ProblemDetails Swagger auto-generates for [ApiController] endpoints.
// Our [ProducesResponseType(typeof(ValidationProblemDetails), 400)] annotations are authoritative.
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
    options.SuppressMapClientErrors = true);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient<IRandomService, RandomService>()
    .AddStandardResilienceHandler(options =>
    {
        // Each individual attempt times out at 3s
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
        // Total across all retries: 3 attempts × 3s + backoff ≈ 10s ceiling
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
        options.Retry.MaxRetryAttempts = 2;
        options.Retry.Delay = TimeSpan.FromMilliseconds(200);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.FailureRatio = 0.5;
    });

// Use Redis when a connection string is configured, fall back to in-memory for local dev without Docker.
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConnectionString))
{
    var multiplexer = ConnectionMultiplexer.Connect(redisConnectionString);
    builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);
    builder.Services.AddSingleton<IGameService, RedisGameService>();
}
else
{
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<IGameService, GameService>();
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(builder.Configuration["Cors:AllowedOrigin"]!)
              .WithHeaders("Content-Type", "X-Player-Id")
              .WithMethods("GET", "POST", "DELETE"));
});

builder.Services.AddRateLimiter(options =>
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

var healthChecks = builder.Services.AddHealthChecks()
    .AddUrlGroup(
        new Uri("https://codechallenge.boohma.com/random"),
        name: "random-api",
        tags: ["ready"]);

if (!string.IsNullOrEmpty(redisConnectionString))
    healthChecks.AddRedis(redisConnectionString, name: "redis", tags: ["ready"]);

var app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");
app.UseRateLimiter();
app.MapControllers();
// /health/live  — is the process up? (load balancer uses this)
// /health/ready — are dependencies reachable? (orchestrator uses this before routing traffic)
app.MapHealthChecks("/health/live", new()
{
    Predicate = _ => false  // no checks — if this responds, the process is alive
});
app.MapHealthChecks("/health/ready", new()
{
    Predicate = check => check.Tags.Contains("ready")
});
app.Run();

// Exposes the implicit Program class for WebApplicationFactory in tests
public partial class Program { }
