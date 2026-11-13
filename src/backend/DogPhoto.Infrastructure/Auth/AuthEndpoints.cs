using DogPhoto.Infrastructure.Persistence.Identity;
using DogPhoto.Infrastructure.RateLimiting;
using DogPhoto.SharedKernel.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace DogPhoto.Infrastructure.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", Register).RequireRateLimiting(RateLimitPolicies.Auth);
        group.MapPost("/login", Login).RequireRateLimiting(RateLimitPolicies.Auth);
        group.MapPost("/refresh", Refresh).RequireRateLimiting(RateLimitPolicies.Auth);
        group.MapGet("/me", Me).RequireAuthorization();
    }

    private static async Task<IResult> Register(
        RegisterRequest request,
        IdentityDbContext db,
        ITokenService tokenService)
    {
        if (await db.Users.AnyAsync(u => u.Email == request.Email))
            return Results.Conflict(new { error = "Email already registered." });

        var user = new User
        {
            Email = request.Email,
            DisplayName = request.DisplayName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = Roles.Customer
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var (accessToken, refreshToken) = await CreateTokens(user, db, tokenService);

        return Results.Ok(new AuthResponse(accessToken, refreshToken, user.Id, user.Email, user.Role));
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        IdentityDbContext db,
        ITokenService tokenService)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user is null || user.PasswordHash is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Results.Unauthorized();

        var (accessToken, refreshToken) = await CreateTokens(user, db, tokenService);

        return Results.Ok(new AuthResponse(accessToken, refreshToken, user.Id, user.Email, user.Role));
    }

    private static async Task<IResult> Refresh(
        RefreshRequest request,
        IdentityDbContext db,
        ITokenService tokenService)
    {
        var storedToken = await db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken);

        if (storedToken is null || !storedToken.IsActive)
            return Results.Unauthorized();

        storedToken.RevokedAt = DateTime.UtcNow;

        var (accessToken, refreshToken) = await CreateTokens(storedToken.User, db, tokenService);

        return Results.Ok(new AuthResponse(accessToken, refreshToken, storedToken.User.Id, storedToken.User.Email, storedToken.User.Role));
    }

    private static async Task<IResult> Me(ICurrentUser currentUser, IdentityDbContext db)
    {
        if (currentUser.UserId is null)
            return Results.Unauthorized();

        var user = await db.Users.FindAsync(currentUser.UserId.Value);
        if (user is null)
            return Results.NotFound();

        return Results.Ok(new { user.Id, user.Email, user.DisplayName, user.Role, user.AvatarUrl });
    }

    private static async Task<(string accessToken, string refreshToken)> CreateTokens(
        User user, IdentityDbContext db, ITokenService tokenService)
    {
        var accessToken = tokenService.GenerateAccessToken(user);
        var refreshTokenValue = tokenService.GenerateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshTokenValue,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });

        await db.SaveChangesAsync();

        return (accessToken, refreshTokenValue);
    }
}

public record RegisterRequest(string Email, string Password, string? DisplayName);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record AuthResponse(string AccessToken, string RefreshToken, Guid UserId, string Email, string Role);
