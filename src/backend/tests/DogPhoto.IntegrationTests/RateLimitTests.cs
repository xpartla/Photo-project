using System.Net;
using System.Net.Http.Json;
using DogPhoto.Infrastructure.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DogPhoto.IntegrationTests;

public sealed class RateLimitTests : ApiTestBase
{
    private const int AuthLimit = 3;
    private const int BookingLimit = 2;
    private const string ClientIpHeader = "X-Test-Client-Ip";

    public RateLimitTests(ApiFactory factory) : base(factory) { }

    /// <summary>
    /// A host with low limits. TestServer has no remote address, so a startup
    /// filter takes it from a header, letting each test act as distinct clients.
    /// </summary>
    private HttpClient CreateLimitedClient(string clientIp)
    {
        var client = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.Configure<RateLimitSettings>(settings =>
            {
                settings.Auth = new RateLimitRule { PermitLimit = AuthLimit, WindowSeconds = 300 };
                settings.Booking = new RateLimitRule { PermitLimit = BookingLimit, WindowSeconds = 300 };
            });
            services.AddSingleton<IStartupFilter, ClientIpFromHeaderFilter>();
        })).CreateClient();
        client.DefaultRequestHeaders.Add(ClientIpHeader, clientIp);
        return client;
    }

    private static Task<HttpResponseMessage> FailedLoginAsync(HttpClient client) =>
        client.PostAsJsonAsync("/api/auth/login", new { email = "nobody@example.test", password = "wrong" });

    [Fact]
    public async Task Login_OverTheLimit_Returns429WithRetryAfter()
    {
        var client = CreateLimitedClient("203.0.113.10");

        for (var i = 0; i < AuthLimit; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await FailedLoginAsync(client)).StatusCode);

        var rejected = await FailedLoginAsync(client);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.Contains("Retry-After"));
        var body = await rejected.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.False(string.IsNullOrWhiteSpace(body?.Error));
    }

    [Fact]
    public async Task Login_LimitIsPerClientAddress()
    {
        var first = CreateLimitedClient("203.0.113.20");
        for (var i = 0; i <= AuthLimit; i++) await FailedLoginAsync(first);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await FailedLoginAsync(first)).StatusCode);

        var second = CreateLimitedClient("203.0.113.21");
        Assert.Equal(HttpStatusCode.Unauthorized, (await FailedLoginAsync(second)).StatusCode);
    }

    [Fact]
    public async Task RegisterAndRefresh_ShareTheAuthLimit()
    {
        var client = CreateLimitedClient("203.0.113.30");
        for (var i = 0; i < AuthLimit; i++) await FailedLoginAsync(client);

        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"limited-{Guid.NewGuid():N}@example.test",
            password = "customer-pw-1234"
        });
        Assert.Equal(HttpStatusCode.TooManyRequests, register.StatusCode);

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = "anything" });
        Assert.Equal(HttpStatusCode.TooManyRequests, refresh.StatusCode);
    }

    [Fact]
    public async Task CreateBooking_OverTheLimit_Returns429()
    {
        var client = CreateLimitedClient("203.0.113.40");
        var invalidBooking = new { sessionTypeId = Guid.NewGuid(), clientName = "", clientEmail = "" };

        for (var i = 0; i < BookingLimit; i++)
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/booking/bookings", invalidBooking)).StatusCode);

        var rejected = await client.PostAsJsonAsync("/api/booking/bookings", invalidBooking);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }

    [Fact]
    public async Task UnlimitedEndpoints_AreNotThrottled()
    {
        var client = CreateLimitedClient("203.0.113.50");
        for (var i = 0; i < AuthLimit + BookingLimit + 2; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/booking/session-types")).StatusCode);
    }

    private sealed record ErrorBody(string Error);

    private sealed class ClientIpFromHeaderFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                if (context.Request.Headers.TryGetValue(ClientIpHeader, out var ip) && IPAddress.TryParse(ip, out var parsed))
                    context.Connection.RemoteIpAddress = parsed;
                await nextMiddleware();
            });
            next(app);
        };
    }
}
