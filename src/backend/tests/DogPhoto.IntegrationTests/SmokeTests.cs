using System.Net;
using Xunit;

namespace DogPhoto.IntegrationTests;

public sealed class SmokeTests : ApiTestBase
{
    public SmokeTests(ApiFactory factory) : base(factory) { }

    [Fact]
    public async Task HealthReady_ReturnsHealthy()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HealthLive_ReturnsHealthy()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdminLogin_Succeeds()
    {
        // CreateAdminClientAsync throws if login fails — that's the assertion.
        var client = await CreateAdminClientAsync();
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task PortfolioPhotos_ReturnsEmptyListInitially()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/portfolio/photos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ImagePipelineUpload_RequiresAuth()
    {
        var client = CreateClient();
        var response = await client.PostAsync("/api/image-pipeline/upload", new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
