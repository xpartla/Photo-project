using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DogPhoto.Infrastructure.RateLimiting;

public static class RateLimitPolicies
{
    public const string Auth = "auth";
    public const string Booking = "booking";
}

public static class RateLimitingExtensions
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitSettings>(configuration.GetSection(RateLimitSettings.SectionName));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { error = "Too many requests. Please try again in a moment." }, cancellationToken);
            };

            options.AddPolicy(RateLimitPolicies.Auth, context => PerClientIp(context, s => s.Auth));
            options.AddPolicy(RateLimitPolicies.Booking, context => PerClientIp(context, s => s.Booking));
        });

        return services;
    }

    // Settings are resolved per request so a host can override them (tests do).
    // Behind a reverse proxy the client address is only correct when forwarded
    // headers are enabled (ASPNETCORE_FORWARDEDHEADERS_ENABLED=true); without
    // that every visitor shares the proxy's address and therefore one bucket.
    private static RateLimitPartition<string> PerClientIp(HttpContext context, Func<RateLimitSettings, RateLimitRule> pick)
    {
        var rule = pick(context.RequestServices.GetRequiredService<IOptions<RateLimitSettings>>().Value);
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rule.PermitLimit,
            Window = TimeSpan.FromSeconds(rule.WindowSeconds),
            QueueLimit = 0
        });
    }
}
