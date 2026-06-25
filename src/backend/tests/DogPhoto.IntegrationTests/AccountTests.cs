using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DogPhoto.IntegrationTests;

public sealed class AccountTests : ApiTestBase
{
    public AccountTests(ApiFactory factory) : base(factory) { }

    private static object SampleAddress(string label = "Home", bool? defaultShipping = null, bool? defaultBilling = null) => new
    {
        label,
        name = "Adam Partl",
        street = "Tichá 12",
        city = "Bratislava",
        postalCode = "81101",
        country = "SK",
        isDefaultShipping = defaultShipping,
        isDefaultBilling = defaultBilling
    };

    // ── Profile ────────────────────────────────────────────────────

    [Fact]
    public async Task Profile_RequiresAuth()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/account/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_PersistsDisplayNameAndPhone()
    {
        var customer = await CreateCustomerClientAsync();

        var update = await customer.PutAsJsonAsync("/api/account/profile", new
        {
            displayName = "Updated Name",
            phone = "+421 905 111 222"
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var profile = await customer.GetFromJsonAsync<JsonElement>("/api/account/profile");
        Assert.Equal("Updated Name", profile.GetProperty("displayName").GetString());
        Assert.Equal("+421 905 111 222", profile.GetProperty("phone").GetString());
    }

    [Fact]
    public async Task ChangePassword_RequiresCurrentPassword()
    {
        var customer = await CreateCustomerClientAsync();

        // Wrong current password is rejected.
        var wrong = await customer.PutAsJsonAsync("/api/account/profile", new
        {
            currentPassword = "not-the-real-password",
            newPassword = "brand-new-password-123"
        });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        // The seeded customer used CreateCustomerClientAsync which registers with
        // password "customer-pw-1234" — that should let us set a new password.
        var ok = await customer.PutAsJsonAsync("/api/account/profile", new
        {
            currentPassword = "customer-pw-1234",
            newPassword = "brand-new-password-123"
        });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_TooShort_Returns400()
    {
        var customer = await CreateCustomerClientAsync();
        var resp = await customer.PutAsJsonAsync("/api/account/profile", new
        {
            currentPassword = "customer-pw-1234",
            newPassword = "short"
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ── Addresses ──────────────────────────────────────────────────

    [Fact]
    public async Task Addresses_RequireAuth()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/account/addresses");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FirstAddress_AutoDefaultsToBoth()
    {
        var customer = await CreateCustomerClientAsync();

        var created = await customer.PostAsJsonAsync("/api/account/addresses", SampleAddress());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var addr = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(addr.GetProperty("isDefaultShipping").GetBoolean());
        Assert.True(addr.GetProperty("isDefaultBilling").GetBoolean());
    }

    [Fact]
    public async Task SetDefault_ClearsPreviousDefault()
    {
        var customer = await CreateCustomerClientAsync();

        var first = await customer.PostAsJsonAsync("/api/account/addresses", SampleAddress("Home"));
        var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var second = await customer.PostAsJsonAsync("/api/account/addresses", SampleAddress("Work"));
        var secondId = (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        // Promote second as the new default-shipping; the previous default should flip off.
        var promote = await customer.PutAsJsonAsync(
            $"/api/account/addresses/{secondId}/default",
            new { shipping = true });
        Assert.Equal(HttpStatusCode.OK, promote.StatusCode);

        var listed = await customer.GetFromJsonAsync<JsonElement>("/api/account/addresses");
        var defaultsForShipping = listed.EnumerateArray()
            .Where(a => a.GetProperty("isDefaultShipping").GetBoolean())
            .Select(a => a.GetProperty("id").GetString())
            .ToList();
        Assert.Single(defaultsForShipping);
        Assert.Equal(secondId, defaultsForShipping[0]);
    }

    [Fact]
    public async Task DeletingDefault_PromotesAnotherAddress()
    {
        var customer = await CreateCustomerClientAsync();
        var first = await customer.PostAsJsonAsync("/api/account/addresses", SampleAddress("Home"));
        var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        var second = await customer.PostAsJsonAsync("/api/account/addresses", SampleAddress("Work"));
        var secondId = (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var del = await customer.DeleteAsync($"/api/account/addresses/{firstId}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var listed = await customer.GetFromJsonAsync<JsonElement>("/api/account/addresses");
        Assert.Single(listed.EnumerateArray());
        var remaining = listed.EnumerateArray().Single();
        Assert.Equal(secondId, remaining.GetProperty("id").GetString());
        Assert.True(remaining.GetProperty("isDefaultShipping").GetBoolean());
        Assert.True(remaining.GetProperty("isDefaultBilling").GetBoolean());
    }

    [Fact]
    public async Task UserCannotAccessAnotherUsersAddresses()
    {
        var customerA = await CreateCustomerClientAsync();
        var created = await customerA.PostAsJsonAsync("/api/account/addresses", SampleAddress());
        var addressId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var customerB = await CreateCustomerClientAsync();
        var listB = await customerB.GetFromJsonAsync<JsonElement>("/api/account/addresses");
        Assert.Empty(listB.EnumerateArray());

        // B cannot update A's address — endpoint scopes by current user, returning 404.
        var update = await customerB.PutAsJsonAsync(
            $"/api/account/addresses/{addressId}",
            SampleAddress("Hijack"));
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);

        var del = await customerB.DeleteAsync($"/api/account/addresses/{addressId}");
        Assert.Equal(HttpStatusCode.NotFound, del.StatusCode);
    }

    [Fact]
    public async Task CreateAddress_Validation_RequiresFields()
    {
        var customer = await CreateCustomerClientAsync();
        var resp = await customer.PostAsJsonAsync("/api/account/addresses", new
        {
            name = "",
            street = "",
            city = "",
            postalCode = ""
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
