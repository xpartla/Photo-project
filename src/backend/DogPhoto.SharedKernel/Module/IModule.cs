using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DogPhoto.SharedKernel.Module;

public interface IModule
{
    static abstract string Name { get; }
    static abstract void AddServices(IServiceCollection services, IConfiguration configuration);
    static abstract void MapEndpoints(IEndpointRouteBuilder endpoints);
}
