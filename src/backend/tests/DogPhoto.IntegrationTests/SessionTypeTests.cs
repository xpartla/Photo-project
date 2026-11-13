using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DogPhoto.IntegrationTests;

/// <summary>
/// Booking packages are owned by the admin UI: the startup seed only adds
/// starter packages that are missing and never rewrites existing rows.
/// </summary>
public sealed class SessionTypeTests : ApiTestBase
{
    public SessionTypeTests(ApiFactory factory) : base(factory) { }

    private static string NewSlug() => $"pkg-{Guid.NewGuid():N}";

    // Priced far above the starter packages so tests that pick the cheapest
    // package keep getting a seeded one.
    private static object NewPackage(string slug) => new
    {
        slug,
        nameSk = "Testovací balík",
        nameEn = "Test package",
        descriptionSk = "Popis",
        descriptionEn = "Description",
        durationMinutes = 45,
        basePrice = 9000m,
        category = "digital",
        maxDogs = 2,
        includesSk = new[] { "10 fotografií", "online galéria" },
        includesEn = new[] { "10 photos", "online gallery" },
    };

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private async Task<JsonElement> CreateAsync(HttpClient admin, string slug)
    {
        var response = await admin.PostAsJsonAsync("/api/booking/session-types", NewPackage(slug));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync(response);
    }

    private static async Task<JsonElement?> FindAsync(HttpClient client, string slug, string query = "")
    {
        var list = await ReadAsync(await client.GetAsync($"/api/booking/session-types{query}"));
        foreach (var item in list.EnumerateArray())
            if (item.GetProperty("slug").GetString() == slug) return item;
        return null;
    }

    private static string[] Strings(JsonElement array) =>
        array.EnumerateArray().Select(e => e.GetString()!).ToArray();

    // ── Create ────────────────────────────────────────────────────

    [Fact]
    public async Task Create_RequiresAdmin()
    {
        var anonymous = await CreateClient().PostAsJsonAsync("/api/booking/session-types", NewPackage(NewSlug()));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var customer = await CreateCustomerClientAsync();
        var forbidden = await customer.PostAsJsonAsync("/api/booking/session-types", NewPackage(NewSlug()));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Create_ThenListedPublicly_WithLocalizedIncludes()
    {
        var admin = await CreateAdminClientAsync();
        var slug = NewSlug();
        await CreateAsync(admin, slug);

        var sk = await FindAsync(CreateClient(), slug, "?lang=sk");
        var en = await FindAsync(CreateClient(), slug, "?lang=en");

        Assert.Equal(new[] { "10 fotografií", "online galéria" }, Strings(sk!.Value.GetProperty("includes")));
        Assert.Equal(new[] { "10 photos", "online gallery" }, Strings(en!.Value.GetProperty("includes")));
        Assert.Equal("Testovací balík", sk.Value.GetProperty("name").GetString());
        Assert.Equal("Test package", en.Value.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Create_DuplicateSlug_Returns409()
    {
        var admin = await CreateAdminClientAsync();
        var slug = NewSlug();
        await CreateAsync(admin, slug);

        var duplicate = await admin.PostAsJsonAsync("/api/booking/session-types", NewPackage(slug));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Theory]
    [InlineData("Has Spaces")]
    [InlineData("UPPER")]
    [InlineData("trailing-")]
    [InlineData("")]
    public async Task Create_InvalidSlug_Returns400(string slug)
    {
        var admin = await CreateAdminClientAsync();
        var response = await admin.PostAsJsonAsync("/api/booking/session-types", NewPackage(slug));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0, 100, 1)]
    [InlineData(60, -1, 1)]
    [InlineData(60, 100, 0)]
    public async Task Create_InvalidNumbers_Returns400(int durationMinutes, int basePrice, int maxDogs)
    {
        var admin = await CreateAdminClientAsync();
        var response = await admin.PostAsJsonAsync("/api/booking/session-types", new
        {
            slug = NewSlug(),
            nameSk = "X",
            nameEn = "X",
            durationMinutes,
            basePrice,
            maxDogs,
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Update ────────────────────────────────────────────────────

    [Fact]
    public async Task Update_RequiresAdmin()
    {
        var admin = await CreateAdminClientAsync();
        var id = (await CreateAsync(admin, NewSlug())).GetProperty("id").GetString();

        var customer = await CreateCustomerClientAsync();
        var response = await customer.PutAsJsonAsync($"/api/booking/session-types/{id}", new { basePrice = 1m });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_ChangesOnlyTheFieldsSent()
    {
        var admin = await CreateAdminClientAsync();
        var slug = NewSlug();
        var id = (await CreateAsync(admin, slug)).GetProperty("id").GetString();

        var response = await admin.PutAsJsonAsync($"/api/booking/session-types/{id}", new { basePrice = 9500m });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = (await FindAsync(CreateClient(), slug, "?lang=en"))!.Value;
        Assert.Equal(9500m, updated.GetProperty("basePrice").GetDecimal());
        Assert.Equal(45, updated.GetProperty("durationMinutes").GetInt32());
        Assert.Equal("Test package", updated.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Update_OneLanguageOfIncludes_KeepsTheOther()
    {
        var admin = await CreateAdminClientAsync();
        var slug = NewSlug();
        var id = (await CreateAsync(admin, slug)).GetProperty("id").GetString();

        await admin.PutAsJsonAsync($"/api/booking/session-types/{id}", new { includesEn = new[] { "20 photos" } });

        var item = (await FindAsync(CreateClient(), slug))!.Value;
        Assert.Equal(new[] { "10 fotografií", "online galéria" }, Strings(item.GetProperty("includesSk")));
        Assert.Equal(new[] { "20 photos" }, Strings(item.GetProperty("includesEn")));
    }

    [Fact]
    public async Task Update_InvalidNumbers_Returns400()
    {
        var admin = await CreateAdminClientAsync();
        var id = (await CreateAsync(admin, NewSlug())).GetProperty("id").GetString();

        var response = await admin.PutAsJsonAsync($"/api/booking/session-types/{id}", new { durationMinutes = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Deactivation ──────────────────────────────────────────────

    [Fact]
    public async Task Deactivated_IsHiddenFromPublic_ButListedForAdmin()
    {
        var admin = await CreateAdminClientAsync();
        var slug = NewSlug();
        var id = (await CreateAsync(admin, slug)).GetProperty("id").GetString();
        await admin.PutAsJsonAsync($"/api/booking/session-types/{id}", new { isActive = false });

        Assert.Null(await FindAsync(CreateClient(), slug));
        Assert.Null(await FindAsync(CreateClient(), slug, "?includeInactive=true"));
        Assert.Null(await FindAsync(await CreateCustomerClientAsync(), slug, "?includeInactive=true"));
        Assert.Equal(HttpStatusCode.NotFound,
            (await CreateClient().GetAsync($"/api/booking/session-types/{slug}")).StatusCode);

        var forAdmin = await FindAsync(admin, slug, "?includeInactive=true");
        Assert.NotNull(forAdmin);
        Assert.False(forAdmin!.Value.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task Deactivated_CannotBeBooked()
    {
        var admin = await CreateAdminClientAsync();
        var id = (await CreateAsync(admin, NewSlug())).GetProperty("id").GetString();
        await admin.PutAsJsonAsync($"/api/booking/session-types/{id}", new { isActive = false });

        var response = await CreateClient().PostAsJsonAsync("/api/booking/bookings", new
        {
            sessionTypeId = id,
            clientName = "Jana",
            clientEmail = "jana@example.test",
            dogCount = 1,
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Startup seed ──────────────────────────────────────────────

    [Fact]
    public async Task Restart_KeepsAdminChanges_AndDoesNotTouchCustomPackages()
    {
        var admin = await CreateAdminClientAsync();

        // A custom package, and a starter package edited by the admin.
        var customSlug = NewSlug();
        await CreateAsync(admin, customSlug);

        var action = (await FindAsync(admin, "action", "?lang=en"))!.Value;
        var actionId = action.GetProperty("id").GetString();
        var originalDescription = action.GetProperty("descriptionEn").GetString();
        const string edited = "Edited by the admin.";

        try
        {
            await admin.PutAsJsonAsync($"/api/booking/session-types/{actionId}", new { descriptionEn = edited });

            // A second host runs the startup seed again against the same database.
            using var restarted = Factory.WithWebHostBuilder(_ => { });
            var client = restarted.CreateClient();

            var actionAfter = (await FindAsync(client, "action", "?lang=en"))!.Value;
            Assert.Equal(edited, actionAfter.GetProperty("descriptionEn").GetString());

            var customAfter = await FindAsync(client, customSlug);
            Assert.NotNull(customAfter);
            Assert.True(customAfter!.Value.GetProperty("isActive").GetBoolean());
        }
        finally
        {
            await admin.PutAsJsonAsync($"/api/booking/session-types/{actionId}", new { descriptionEn = originalDescription });
        }
    }

    [Fact]
    public async Task StarterPackages_AreSeededOnce()
    {
        var list = await ReadAsync(await CreateClient().GetAsync("/api/booking/session-types"));
        var slugs = list.EnumerateArray().Select(e => e.GetProperty("slug").GetString()).ToList();

        foreach (var starter in new[] { "legacy", "digital", "action" })
            Assert.Single(slugs, s => s == starter);
    }
}
