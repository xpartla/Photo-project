using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace DogPhoto.IntegrationTests;

public sealed class AuthTests : ApiTestBase
{
    public AuthTests(ApiFactory factory) : base(factory) { }

    // The old endpoint issued tokens for whatever e-mail the caller claimed.
    // Guard against it being reintroduced without real ID-token verification.
    [Fact]
    public async Task GoogleLogin_WithClaimedAdminEmail_IssuesNoTokens()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/google", new
        {
            idToken = "forged",
            email = "admin@dogphoto.sk",
            displayName = "Admin",
            subject = "forged-subject"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@dogphoto.sk",
            password = "not-the-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_RotatesToken_AndRejectsReuse()
    {
        var client = CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "customer@dogphoto.sk",
            password = "customer123"
        });
        login.EnsureSuccessStatusCode();
        var first = await login.Content.ReadFromJsonAsync<Tokens>();

        var refreshed = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first!.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var second = await refreshed.Content.ReadFromJsonAsync<Tokens>();
        Assert.NotEqual(first.RefreshToken, second!.RefreshToken);

        var reused = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
    }

    private sealed record Tokens(string AccessToken, string RefreshToken);
}
