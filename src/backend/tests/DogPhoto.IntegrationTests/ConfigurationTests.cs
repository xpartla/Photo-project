using DogPhoto.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Xunit;

namespace DogPhoto.IntegrationTests;

/// <summary>
/// Startup configuration checks. These build the service collection only, so
/// they need no containers and are not part of the shared API collection.
/// </summary>
public sealed class ConfigurationTests
{
    private static readonly IHostEnvironment Development =
        new HostingEnvironment { EnvironmentName = Environments.Development };

    private static IConfiguration Config(string? jwtSecret)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
        };
        if (jwtSecret is not null) values["Jwt:Secret"] = jwtSecret;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void AddInfrastructure_WithoutJwtSecret_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddInfrastructure(Config(jwtSecret: null), Development));
        Assert.Contains("Jwt:Secret", ex.Message);
    }

    [Fact]
    public void AddInfrastructure_WithShortJwtSecret_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddInfrastructure(Config(jwtSecret: "too-short"), Development));
    }

    [Fact]
    public void AddInfrastructure_WithValidJwtSecret_Succeeds()
    {
        new ServiceCollection().AddInfrastructure(Config(jwtSecret: new string('x', 32)), Development);
    }
}
