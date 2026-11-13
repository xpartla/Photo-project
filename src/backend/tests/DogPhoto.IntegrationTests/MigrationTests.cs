using DogPhoto.Infrastructure.Persistence.Blog;
using DogPhoto.Infrastructure.Persistence.Booking;
using DogPhoto.Infrastructure.Persistence.EShop;
using DogPhoto.Infrastructure.Persistence.Identity;
using DogPhoto.Infrastructure.Persistence.Portfolio;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DogPhoto.IntegrationTests;

public sealed class MigrationTests : ApiTestBase
{
    public MigrationTests(ApiFactory factory) : base(factory) { }

    public static TheoryData<Type> Contexts => new()
    {
        typeof(IdentityDbContext),
        typeof(PortfolioDbContext),
        typeof(EShopDbContext),
        typeof(BookingDbContext),
        typeof(BlogDbContext),
    };

    // Fails when an entity or its mapping changed without `make migration`.
    [Theory]
    [MemberData(nameof(Contexts))]
    public void Model_HasNoChangesMissingFromMigrations(Type contextType)
    {
        using var scope = Factory.Services.CreateScope();
        var context = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);

        Assert.False(context.Database.HasPendingModelChanges(),
            $"{contextType.Name} differs from its migration snapshot. Add a migration.");
    }

    [Theory]
    [MemberData(nameof(Contexts))]
    public async Task Database_HasAllMigrationsApplied(Type contextType)
    {
        using var scope = Factory.Services.CreateScope();
        var context = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);

        Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }
}
