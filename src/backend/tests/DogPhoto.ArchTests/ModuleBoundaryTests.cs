using NetArchTest.Rules;
using Xunit;

namespace DogPhoto.ArchTests;

public class ModuleBoundaryTests
{
    [Fact]
    public void SharedKernel_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(typeof(SharedKernel.Auth.ICurrentUser).Assembly)
            .ShouldNot()
            .HaveDependencyOn("DogPhoto.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "SharedKernel must not depend on Infrastructure.");
    }

    [Fact]
    public void SharedKernel_ShouldNotDependOn_Api()
    {
        var result = Types.InAssembly(typeof(SharedKernel.Auth.ICurrentUser).Assembly)
            .ShouldNot()
            .HaveDependencyOn("DogPhoto.Api")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "SharedKernel must not depend on Api.");
    }

    [Fact]
    public void Infrastructure_ShouldNotDependOn_Api()
    {
        var result = Types.InAssembly(typeof(Infrastructure.DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOn("DogPhoto.Api")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Infrastructure must not depend on Api.");
    }

    [Fact]
    public void BookingModule_ShouldNotDependOn_EShopOrBlog()
    {
        var result = Types.InAssembly(typeof(Infrastructure.DependencyInjection).Assembly)
            .That()
            .ResideInNamespaceContaining("Booking")
            .ShouldNot()
            .HaveDependencyOnAny("DogPhoto.Infrastructure.Persistence.EShop", "DogPhoto.Infrastructure.Persistence.Blog")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Booking module must not directly reference EShop or Blog.");
    }

    [Fact]
    public void Infrastructure_ShouldDependOn_SharedKernel()
    {
        // Verify that CurrentUser implements ICurrentUser from SharedKernel
        var currentUserType = typeof(Infrastructure.Auth.CurrentUser);
        Assert.True(
            typeof(SharedKernel.Auth.ICurrentUser).IsAssignableFrom(currentUserType),
            "Infrastructure.Auth.CurrentUser should implement SharedKernel.Auth.ICurrentUser.");
    }
}
