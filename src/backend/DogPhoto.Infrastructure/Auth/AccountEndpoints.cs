using DogPhoto.Infrastructure.Persistence.Identity;
using DogPhoto.SharedKernel.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace DogPhoto.Infrastructure.Auth;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/account").WithTags("Account").RequireAuthorization();

        // ── Profile ────────────────────────────────────────────────────

        group.MapGet("/profile", async (ICurrentUser currentUser, IdentityDbContext db) =>
        {
            if (currentUser.UserId is null) return Results.Unauthorized();

            var user = await db.Users.FindAsync(currentUser.UserId.Value);
            if (user is null) return Results.NotFound();

            return Results.Ok(new
            {
                id = user.Id,
                email = user.Email,
                displayName = user.DisplayName,
                phone = user.Phone,
                role = user.Role,
                hasPassword = user.PasswordHash is not null,
                oauthProvider = user.OAuthProvider
            });
        });

        group.MapPut("/profile", async (
            UpdateProfileRequest request,
            ICurrentUser currentUser,
            IdentityDbContext db) =>
        {
            if (currentUser.UserId is null) return Results.Unauthorized();

            var user = await db.Users.FindAsync(currentUser.UserId.Value);
            if (user is null) return Results.NotFound();

            if (request.DisplayName is not null) user.DisplayName = request.DisplayName;
            if (request.Phone is not null) user.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone;

            // Password change: existing password required when one is set.
            if (!string.IsNullOrEmpty(request.NewPassword))
            {
                if (request.NewPassword.Length < 8)
                    return Results.BadRequest(new { error = "Password must be at least 8 characters." });

                if (user.PasswordHash is not null)
                {
                    if (string.IsNullOrEmpty(request.CurrentPassword) ||
                        !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
                        return Results.BadRequest(new { error = "Current password is incorrect." });
                }

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            }

            user.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                id = user.Id,
                email = user.Email,
                displayName = user.DisplayName,
                phone = user.Phone
            });
        });

        // ── Addresses ──────────────────────────────────────────────────

        group.MapGet("/addresses", async (ICurrentUser currentUser, IdentityDbContext db) =>
        {
            if (currentUser.UserId is null) return Results.Unauthorized();

            var addresses = await db.Addresses
                .Where(a => a.UserId == currentUser.UserId.Value)
                .OrderByDescending(a => a.IsDefaultShipping)
                .ThenByDescending(a => a.IsDefaultBilling)
                .ThenBy(a => a.CreatedAt)
                .ToListAsync();

            return Results.Ok(addresses.Select(MapAddress));
        });

        group.MapPost("/addresses", async (
            AddressRequest request,
            ICurrentUser currentUser,
            IdentityDbContext db) =>
        {
            if (currentUser.UserId is null) return Results.Unauthorized();

            var validation = ValidateAddress(request);
            if (validation is not null) return Results.BadRequest(new { error = validation });

            var userId = currentUser.UserId.Value;
            var hasAny = await db.Addresses.AnyAsync(a => a.UserId == userId);

            // First saved address auto-defaults to both, regardless of caller's flags.
            var asShipping = !hasAny || (request.IsDefaultShipping ?? false);
            var asBilling = !hasAny || (request.IsDefaultBilling ?? false);

            if (asShipping)
                await db.Addresses
                    .Where(a => a.UserId == userId && a.IsDefaultShipping)
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefaultShipping, false));
            if (asBilling)
                await db.Addresses
                    .Where(a => a.UserId == userId && a.IsDefaultBilling)
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefaultBilling, false));

            var address = new Address
            {
                UserId = userId,
                Label = request.Label,
                Name = request.Name,
                Street = request.Street,
                City = request.City,
                PostalCode = request.PostalCode,
                Country = string.IsNullOrWhiteSpace(request.Country) ? "SK" : request.Country!,
                IsDefaultShipping = asShipping,
                IsDefaultBilling = asBilling
            };

            db.Addresses.Add(address);
            await db.SaveChangesAsync();

            return Results.Created($"/api/account/addresses/{address.Id}", MapAddress(address));
        });

        group.MapPut("/addresses/{id:guid}", async (
            Guid id,
            AddressRequest request,
            ICurrentUser currentUser,
            IdentityDbContext db) =>
        {
            if (currentUser.UserId is null) return Results.Unauthorized();

            var address = await db.Addresses
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == currentUser.UserId.Value);
            if (address is null) return Results.NotFound();

            var validation = ValidateAddress(request);
            if (validation is not null) return Results.BadRequest(new { error = validation });

            address.Label = request.Label;
            address.Name = request.Name;
            address.Street = request.Street;
            address.City = request.City;
            address.PostalCode = request.PostalCode;
            address.Country = string.IsNullOrWhiteSpace(request.Country) ? "SK" : request.Country!;
            address.UpdatedAt = DateTime.UtcNow;

            // Default flags can also be flipped via the dedicated /default endpoint;
            // accept them here too so a single PUT can update everything in one call.
            if (request.IsDefaultShipping == true && !address.IsDefaultShipping)
            {
                await db.Addresses
                    .Where(a => a.UserId == address.UserId && a.IsDefaultShipping && a.Id != address.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefaultShipping, false));
                address.IsDefaultShipping = true;
            }
            if (request.IsDefaultBilling == true && !address.IsDefaultBilling)
            {
                await db.Addresses
                    .Where(a => a.UserId == address.UserId && a.IsDefaultBilling && a.Id != address.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefaultBilling, false));
                address.IsDefaultBilling = true;
            }

            await db.SaveChangesAsync();
            return Results.Ok(MapAddress(address));
        });

        group.MapDelete("/addresses/{id:guid}", async (
            Guid id,
            ICurrentUser currentUser,
            IdentityDbContext db) =>
        {
            if (currentUser.UserId is null) return Results.Unauthorized();

            var address = await db.Addresses
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == currentUser.UserId.Value);
            if (address is null) return Results.NotFound();

            db.Addresses.Remove(address);
            await db.SaveChangesAsync();

            // If we removed a default, promote the most recently created remaining address
            // to fill the gap so checkout always has something to pre-fill.
            if (address.IsDefaultShipping || address.IsDefaultBilling)
            {
                var fallback = await db.Addresses
                    .Where(a => a.UserId == address.UserId)
                    .OrderByDescending(a => a.UpdatedAt)
                    .FirstOrDefaultAsync();
                if (fallback is not null)
                {
                    if (address.IsDefaultShipping) fallback.IsDefaultShipping = true;
                    if (address.IsDefaultBilling) fallback.IsDefaultBilling = true;
                    await db.SaveChangesAsync();
                }
            }

            return Results.NoContent();
        });

        group.MapPut("/addresses/{id:guid}/default", async (
            Guid id,
            SetDefaultRequest request,
            ICurrentUser currentUser,
            IdentityDbContext db) =>
        {
            if (currentUser.UserId is null) return Results.Unauthorized();

            var userId = currentUser.UserId.Value;
            var address = await db.Addresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
            if (address is null) return Results.NotFound();

            if (request.Shipping == true)
            {
                await db.Addresses
                    .Where(a => a.UserId == userId && a.IsDefaultShipping && a.Id != id)
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefaultShipping, false));
                address.IsDefaultShipping = true;
            }
            if (request.Billing == true)
            {
                await db.Addresses
                    .Where(a => a.UserId == userId && a.IsDefaultBilling && a.Id != id)
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefaultBilling, false));
                address.IsDefaultBilling = true;
            }
            address.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(MapAddress(address));
        });
    }

    private static string? ValidateAddress(AddressRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return "Name is required.";
        if (string.IsNullOrWhiteSpace(r.Street)) return "Street is required.";
        if (string.IsNullOrWhiteSpace(r.City)) return "City is required.";
        if (string.IsNullOrWhiteSpace(r.PostalCode)) return "Postal code is required.";
        return null;
    }

    private static object MapAddress(Address a) => new
    {
        id = a.Id,
        label = a.Label,
        name = a.Name,
        street = a.Street,
        city = a.City,
        postalCode = a.PostalCode,
        country = a.Country,
        isDefaultShipping = a.IsDefaultShipping,
        isDefaultBilling = a.IsDefaultBilling,
        createdAt = a.CreatedAt,
        updatedAt = a.UpdatedAt
    };
}

public record UpdateProfileRequest(
    string? DisplayName = null,
    string? Phone = null,
    string? CurrentPassword = null,
    string? NewPassword = null);

public record AddressRequest(
    string Name,
    string Street,
    string City,
    string PostalCode,
    string? Country = null,
    string? Label = null,
    bool? IsDefaultShipping = null,
    bool? IsDefaultBilling = null);

public record SetDefaultRequest(bool? Shipping = null, bool? Billing = null);
