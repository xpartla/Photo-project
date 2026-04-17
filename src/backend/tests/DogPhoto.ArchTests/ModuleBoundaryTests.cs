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
    public void EShopModule_ShouldNotDependOn_BookingOrBlog()
    {
        var result = Types.InAssembly(typeof(Infrastructure.DependencyInjection).Assembly)
            .That()
            .ResideInNamespaceContaining("EShop")
            .ShouldNot()
            .HaveDependencyOnAny("DogPhoto.Infrastructure.Persistence.Booking", "DogPhoto.Infrastructure.Persistence.Blog", "DogPhoto.Infrastructure.Booking")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "EShop module must not directly reference Booking or Blog.");
    }

    [Fact]
    public void BlogModule_ShouldNotDependOn_EShopOrBooking()
    {
        var result = Types.InAssembly(typeof(Infrastructure.DependencyInjection).Assembly)
            .That()
            .ResideInNamespaceContaining("DogPhoto.Infrastructure.Blog")
            .ShouldNot()
            .HaveDependencyOnAny(
                "DogPhoto.Infrastructure.Persistence.EShop",
                "DogPhoto.Infrastructure.Persistence.Booking",
                "DogPhoto.Infrastructure.EShop",
                "DogPhoto.Infrastructure.Booking")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Blog module must not directly reference EShop or Booking.");
    }

    [Fact]
    public void PaymentGateway_ConsumedOnlyViaInterface()
    {
        // Verify MockPaymentGateway implements IPaymentGateway
        var mockType = typeof(Infrastructure.Payments.MockPaymentGateway);
        Assert.True(
            typeof(Infrastructure.Payments.IPaymentGateway).IsAssignableFrom(mockType),
            "MockPaymentGateway should implement IPaymentGateway.");
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
