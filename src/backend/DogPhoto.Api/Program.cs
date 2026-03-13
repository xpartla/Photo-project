using System.Threading.RateLimiting;
using DogPhoto.Infrastructure;
using DogPhoto.Infrastructure.Auth;
using DogPhoto.SharedKernel.Middleware;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure (DbContexts, Auth, DI)
builder.Services.AddInfrastructure(builder.Configuration);

// Health checks with DB connectivity (using EF Core DbContext check)
builder.Services.AddHealthChecks()
    .AddDbContextCheck<DogPhoto.Infrastructure.Persistence.Identity.IdentityDbContext>(
        name: "database",
        tags: ["ready"]);

// Global exception handling
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                ?? ["http://localhost:4321"])
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
    });
    options.RejectionStatusCode = 429;
});

var app = builder.Build();

// Apply migrations and seed data on startup
await DependencyInjection.ApplyMigrationsAsync(app.Services);
await DependencyInjection.SeedDataAsync(app.Services);

app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Health checks
app.MapHealthChecks("/health/live", new()
{
    Predicate = _ => false // no checks, just confirms app is running
});
app.MapHealthChecks("/health/ready", new()
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapHealthChecks("/health"); // backward compat

// Auth endpoints
app.MapAuthEndpoints();

app.MapGet("/", () => "DogPhoto API");

app.Run();
