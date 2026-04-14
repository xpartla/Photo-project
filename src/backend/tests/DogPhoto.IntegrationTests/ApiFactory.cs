using DogPhoto.SharedKernel.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.Azurite;
using Testcontainers.PostgreSql;
using Xunit;

namespace DogPhoto.IntegrationTests;

/// <summary>
/// Bootstraps the API in-memory against ephemeral Postgres + Azurite containers.
/// One container set is shared by the whole test suite via <see cref="ApiCollection"/>.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("dogphoto_test")
        .WithUsername("dogphoto")
        .WithPassword("dogphoto_test")
        .Build();

    private readonly AzuriteContainer _azurite = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:latest")
        .WithCommand("--skipApiVersionCheck")
        .Build();

    // Azurite's well-known dev-storage account key.
    private const string AzuriteAccountName = "devstoreaccount1";
    private const string AzuriteAccountKey =
        "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

    /// <summary>
    /// Build the Postgres connection string using the explicit loopback IP and the
    /// container's mapped port. We deliberately bypass <c>localhost</c> because
    /// WSL2 + .NET 10 RC2 occasionally returns <c>EAI_AGAIN</c> from
    /// <c>getaddrinfo("localhost")</c>, and the container is only bound to the
    /// IPv4 loopback so an accidental <c>::1</c> resolution would fail anyway.
    /// </summary>
    private string BuildPostgresConnectionString() =>
        $"Host=127.0.0.1;Port={_postgres.GetMappedPublicPort(5432)};" +
        "Database=dogphoto_test;Username=dogphoto;Password=dogphoto_test";

    /// <summary>
    /// Build the Azurite blob connection string with explicit IPv4 endpoints,
    /// for the same reason as <see cref="BuildPostgresConnectionString"/>.
    /// </summary>
    private string BuildAzuriteConnectionString()
    {
        var blobPort = _azurite.GetMappedPublicPort(10000);
        return
            "DefaultEndpointsProtocol=http;" +
            $"AccountName={AzuriteAccountName};" +
            $"AccountKey={AzuriteAccountKey};" +
            $"BlobEndpoint=http://127.0.0.1:{blobPort}/{AzuriteAccountName};";
    }

    public FakeEmailService FakeEmail { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Force Development so DependencyInjection.ApplyMigrationsAsync uses
        // EnsureCreated/CreateTables (no migration files exist yet).
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureTestServices(services =>
        {
            // Replace SMTP email service with in-memory fake for test assertions
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IEmailService));
            if (descriptor is not null) services.Remove(descriptor);
            services.AddSingleton<IEmailService>(FakeEmail);
        });
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _postgres.StartAsync();
        await _azurite.StartAsync();

        // IMPORTANT: configure via process environment variables, NOT via
        // IWebHostBuilder.ConfigureAppConfiguration. The Program.cs entry point
        // calls services.AddInfrastructure(builder.Configuration) at the top
        // level, which reads ConnectionStrings:Default *eagerly* during service
        // registration — before WebApplicationFactory has a chance to apply
        // ConfigureWebHost callbacks. Environment variables, on the other
        // hand, are picked up by WebApplication.CreateBuilder() right at the
        // start, so they win over appsettings.Development.json (which points
        // at the docker-compose `db` hostname that the host network can't
        // resolve).
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", BuildPostgresConnectionString());
        Environment.SetEnvironmentVariable("Azure__BlobStorage__ConnectionString", BuildAzuriteConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Secret", "integration-test-secret-minimum-32-characters!!");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "DogPhoto.Tests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "DogPhoto.Tests");
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        await _azurite.DisposeAsync();

        Environment.SetEnvironmentVariable("ConnectionStrings__Default", null);
        Environment.SetEnvironmentVariable("Azure__BlobStorage__ConnectionString", null);
        Environment.SetEnvironmentVariable("Jwt__Secret", null);
        Environment.SetEnvironmentVariable("Jwt__Issuer", null);
        Environment.SetEnvironmentVariable("Jwt__Audience", null);
    }
}
