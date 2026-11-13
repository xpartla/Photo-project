using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DogPhoto.Infrastructure.Payments;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace DogPhoto.IntegrationTests;

/// <summary>
/// The mock payment flow must not exist outside Development.
/// </summary>
public sealed class PaymentGatewayTests : ApiTestBase
{
    public PaymentGatewayTests(ApiFactory factory) : base(factory) { }

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> ProductionHost() =>
        Factory.WithWebHostBuilder(builder => builder.UseEnvironment(Environments.Production));

    [Fact]
    public void Development_UsesTheMockGateway()
    {
        Assert.IsType<MockPaymentGateway>(Factory.Services.GetRequiredService<IPaymentGateway>());
    }

    [Fact]
    public void Production_HasNoUsableGateway()
    {
        using var host = ProductionHost();
        var gateway = host.Services.GetRequiredService<IPaymentGateway>();

        Assert.IsType<UnavailablePaymentGateway>(gateway);
        Assert.False(gateway.IsAvailable);
    }

    [Fact]
    public async Task Production_PaymentWebhook_IsNotMapped()
    {
        using var host = ProductionHost();
        var response = await host.CreateClient().PostAsJsonAsync("/api/shop/webhooks/payment",
            new { paymentId = "mock_anything", status = "paid" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Production_MockPaymentInfo_IsNotMapped()
    {
        using var host = ProductionHost();
        var response = await host.CreateClient().GetAsync("/api/shop/payments/mock_anything");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Production_CreateOrder_Returns503()
    {
        using var host = ProductionHost();
        var client = host.CreateClient();
        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"prod-{Guid.NewGuid():N}@example.test",
            password = "customer-pw-1234"
        });
        register.EnsureSuccessStatusCode();
        var auth = await register.Content.ReadFromJsonAsync<Tokens>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var response = await client.PostAsJsonAsync("/api/shop/orders", new { });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Development_PaymentWebhook_IsMapped()
    {
        var response = await CreateClient().PostAsJsonAsync("/api/shop/webhooks/payment",
            new { paymentId = "mock_unknown", status = "paid" });

        // Mapped, and answering for an unknown payment — not a routing 404 with an empty body.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Order not found", await response.Content.ReadAsStringAsync());
    }

    private sealed record Tokens(string AccessToken);
}
