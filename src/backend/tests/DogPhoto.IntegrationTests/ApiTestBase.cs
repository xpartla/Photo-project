using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace DogPhoto.IntegrationTests;

/// <summary>
/// Base class for integration tests. Provides an unauthenticated client and
/// helpers to mint authenticated clients for the seeded admin user or for an
/// ad-hoc customer.
/// </summary>
[Collection(ApiCollection.Name)]
public abstract class ApiTestBase
{
    protected ApiFactory Factory { get; }
    protected FakeEmailService FakeEmail => Factory.FakeEmail;

    protected ApiTestBase(ApiFactory factory)
    {
        Factory = factory;
    }

    protected HttpClient CreateClient() => Factory.CreateClient();

    /// <summary>
    /// Returns an HttpClient pre-authenticated as the seeded admin user.
    /// </summary>
    protected async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@dogphoto.sk",
            password = "admin123"
        });
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>()
            ?? throw new InvalidOperationException("admin login returned no body");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    /// <summary>
    /// Registers a fresh customer and returns an authenticated HttpClient.
    /// Each call uses a unique email so tests don't collide.
    /// </summary>
    protected async Task<HttpClient> CreateCustomerClientAsync()
    {
        var client = CreateClient();
        var email = $"customer-{Guid.NewGuid():N}@example.test";
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "customer-pw-1234",
            displayName = "Test Customer"
        });
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>()
            ?? throw new InvalidOperationException("customer registration returned no body");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private sealed record AuthResponse(string AccessToken, string RefreshToken, Guid UserId, string Email, string Role);
}
