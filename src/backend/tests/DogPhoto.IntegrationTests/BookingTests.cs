using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DogPhoto.IntegrationTests;

public sealed class BookingTests : ApiTestBase
{
    public BookingTests(ApiFactory factory) : base(factory) { }

    /// <summary>
    /// Helper: create an availability slot and return its ID plus a session type ID.
    /// Uses unique dates to avoid cross-test collisions.
    /// </summary>
    private async Task<(string SlotId, string SessionTypeId)> CreateSlotAndGetSessionTypeAsync(string date)
    {
        var admin = await CreateAdminClientAsync();
        var slotResponse = await admin.PostAsJsonAsync("/api/booking/availability", new
        {
            date,
            startTime = "09:00",
            endTime = "10:30"
        });
        slotResponse.EnsureSuccessStatusCode();
        var slotBody = await slotResponse.Content.ReadAsStringAsync();
        using var slotDoc = JsonDocument.Parse(slotBody);
        var slotId = slotDoc.RootElement.GetProperty("slots").EnumerateArray().First().GetProperty("id").GetString()!;

        var typesResponse = await CreateClient().GetAsync("/api/booking/session-types");
        var typesBody = await typesResponse.Content.ReadAsStringAsync();
        using var typesDoc = JsonDocument.Parse(typesBody);
        var sessionTypeId = typesDoc.RootElement.EnumerateArray().First().GetProperty("id").GetString()!;

        return (slotId, sessionTypeId);
    }

    // ── Session Types ─────────────────────────────────────────────

    [Fact]
    public async Task GetSessionTypes_ReturnsSeededTypes()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/booking/session-types");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var items = doc.RootElement;
        Assert.True(items.GetArrayLength() >= 3);
    }

    [Fact]
    public async Task GetSessionTypeBySlug_ReturnsDetail()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/booking/session-types/dog-portrait");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("dog-portrait", doc.RootElement.GetProperty("slug").GetString());
        Assert.Equal(60, doc.RootElement.GetProperty("durationMinutes").GetInt32());
    }

    [Fact]
    public async Task GetSessionTypeBySlug_NotFound_Returns404()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/booking/session-types/nonexistent");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Availability ──────────────────────────────────────────────

    [Fact]
    public async Task GetAvailability_EmptyMonth_ReturnsEmptyList()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/booking/availability?month=1&year=2025");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(0, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task AdminCanCreateAvailability_SingleSlot()
    {
        var admin = await CreateAdminClientAsync();
        var response = await admin.PostAsJsonAsync("/api/booking/availability", new
        {
            date = "2026-06-20",
            startTime = "09:00",
            endTime = "10:30"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(1, doc.RootElement.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task AdminCanCreateAvailability_BulkGeneration()
    {
        var admin = await CreateAdminClientAsync();
        var response = await admin.PostAsJsonAsync("/api/booking/availability", new
        {
            date = "2026-07-11",
            startTime = "09:00",
            endTime = "16:00",
            slotDurationMinutes = 90,
            breakMinutes = 0
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        // 09:00-10:30, 10:30-12:00, 12:00-13:30, 13:30-15:00, 15:00-16:30 => but last one ends at 16:30 > 16:00
        // Actually: 09:00-10:30, 10:30-12:00, 12:00-13:30, 13:30-15:00 => 4 slots (15:00+1:30=16:30 > 16:00)
        var count = doc.RootElement.GetProperty("count").GetInt32();
        Assert.True(count >= 4, $"Expected at least 4 bulk slots, got {count}");
    }

    [Fact]
    public async Task GetAvailability_ReturnsCreatedSlots()
    {
        var admin = await CreateAdminClientAsync();
        await admin.PostAsJsonAsync("/api/booking/availability", new
        {
            date = "2026-08-15",
            startTime = "10:00",
            endTime = "12:00"
        });

        var client = CreateClient();
        var response = await client.GetAsync("/api/booking/availability?month=8&year=2026");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.GetArrayLength() >= 1);
    }

    // ── Bookings ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateBooking_ValidData_Returns201()
    {
        // Create a slot first
        var admin = await CreateAdminClientAsync();
        var slotResponse = await admin.PostAsJsonAsync("/api/booking/availability", new
        {
            date = "2026-09-05",
            startTime = "14:00",
            endTime = "15:30"
        });
        slotResponse.EnsureSuccessStatusCode();
        var slotBody = await slotResponse.Content.ReadAsStringAsync();
        using var slotDoc = JsonDocument.Parse(slotBody);
        var slotId = slotDoc.RootElement.GetProperty("slots").EnumerateArray().First().GetProperty("id").GetString();

        // Get a session type ID
        var typesResponse = await CreateClient().GetAsync("/api/booking/session-types");
        var typesBody = await typesResponse.Content.ReadAsStringAsync();
        using var typesDoc = JsonDocument.Parse(typesBody);
        var sessionTypeId = typesDoc.RootElement.EnumerateArray().First().GetProperty("id").GetString();

        // Create booking (anonymous)
        var client = CreateClient();
        var bookingResponse = await client.PostAsJsonAsync("/api/booking/bookings", new
        {
            sessionTypeId,
            slotId,
            clientName = "Test Client",
            clientEmail = "test@example.com",
            clientPhone = "+421900123456",
            dogCount = 1
        });

        Assert.Equal(HttpStatusCode.Created, bookingResponse.StatusCode);
        var bookingBody = await bookingResponse.Content.ReadAsStringAsync();
        using var bookingDoc = JsonDocument.Parse(bookingBody);
        Assert.Equal("Pending", bookingDoc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task CreateBooking_MissingName_Returns400()
    {
        var client = CreateClient();
        var typesResponse = await client.GetAsync("/api/booking/session-types");
        var typesBody = await typesResponse.Content.ReadAsStringAsync();
        using var typesDoc = JsonDocument.Parse(typesBody);
        var sessionTypeId = typesDoc.RootElement.EnumerateArray().First().GetProperty("id").GetString();

        var response = await client.PostAsJsonAsync("/api/booking/bookings", new
        {
            sessionTypeId,
            clientName = "",
            clientEmail = "test@example.com"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateBooking_InvalidSessionType_Returns400()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/booking/bookings", new
        {
            sessionTypeId = Guid.NewGuid(),
            clientName = "Test",
            clientEmail = "test@example.com"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateBooking_AlreadyBookedSlot_Returns409()
    {
        var admin = await CreateAdminClientAsync();

        // Create a slot
        var slotResponse = await admin.PostAsJsonAsync("/api/booking/availability", new
        {
            date = "2026-09-12",
            startTime = "10:00",
            endTime = "11:30"
        });
        slotResponse.EnsureSuccessStatusCode();
        var slotBody = await slotResponse.Content.ReadAsStringAsync();
        using var slotDoc = JsonDocument.Parse(slotBody);
        var slotId = slotDoc.RootElement.GetProperty("slots").EnumerateArray().First().GetProperty("id").GetString();

        // Get session type
        var typesResponse = await CreateClient().GetAsync("/api/booking/session-types");
        var typesBody = await typesResponse.Content.ReadAsStringAsync();
        using var typesDoc = JsonDocument.Parse(typesBody);
        var sessionTypeId = typesDoc.RootElement.EnumerateArray().First().GetProperty("id").GetString();

        // First booking
        var client = CreateClient();
        var first = await client.PostAsJsonAsync("/api/booking/bookings", new
        {
            sessionTypeId, slotId,
            clientName = "First Client", clientEmail = "first@example.com"
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Second booking same slot
        var second = await client.PostAsJsonAsync("/api/booking/bookings", new
        {
            sessionTypeId, slotId,
            clientName = "Second Client", clientEmail = "second@example.com"
        });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    // ── Auth-protected endpoints ──────────────────────────────────

    [Fact]
    public async Task GetMyBookings_Unauthenticated_Returns401()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/booking/my-bookings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminCanUpdateSessionType()
    {
        var admin = await CreateAdminClientAsync();

        // Get existing session type
        var typesResponse = await admin.GetAsync("/api/booking/session-types");
        var typesBody = await typesResponse.Content.ReadAsStringAsync();
        using var typesDoc = JsonDocument.Parse(typesBody);
        var id = typesDoc.RootElement.EnumerateArray().First().GetProperty("id").GetString();

        var response = await admin.PutAsJsonAsync($"/api/booking/session-types/{id}", new
        {
            basePrice = 999.0m
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(999.0m, doc.RootElement.GetProperty("basePrice").GetDecimal());
    }

    [Fact]
    public async Task AdminCanBlockSlot()
    {
        var admin = await CreateAdminClientAsync();

        // Create a slot
        var slotResponse = await admin.PostAsJsonAsync("/api/booking/availability", new
        {
            date = "2026-10-03",
            startTime = "09:00",
            endTime = "10:30"
        });
        slotResponse.EnsureSuccessStatusCode();
        var slotBody = await slotResponse.Content.ReadAsStringAsync();
        using var slotDoc = JsonDocument.Parse(slotBody);
        var slotId = slotDoc.RootElement.GetProperty("slots").EnumerateArray().First().GetProperty("id").GetString();

        // Block it
        var response = await admin.PutAsJsonAsync($"/api/booking/availability/{slotId}", new
        {
            isBlocked = true
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.GetProperty("isBlocked").GetBoolean());
    }

    [Fact]
    public async Task NonAdmin_CannotCreateAvailability()
    {
        var customer = await CreateCustomerClientAsync();
        var response = await customer.PostAsJsonAsync("/api/booking/availability", new
        {
            date = "2026-11-01",
            startTime = "09:00",
            endTime = "10:30"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CancelBooking_Authenticated()
    {
        var admin = await CreateAdminClientAsync();

        // Create a slot
        var slotResponse = await admin.PostAsJsonAsync("/api/booking/availability", new
        {
            date = "2026-09-20",
            startTime = "09:00",
            endTime = "10:30"
        });
        slotResponse.EnsureSuccessStatusCode();
        var slotBody = await slotResponse.Content.ReadAsStringAsync();
        using var slotDoc = JsonDocument.Parse(slotBody);
        var slotId = slotDoc.RootElement.GetProperty("slots").EnumerateArray().First().GetProperty("id").GetString();

        // Get session type
        var typesResponse = await CreateClient().GetAsync("/api/booking/session-types");
        var typesBody = await typesResponse.Content.ReadAsStringAsync();
        using var typesDoc = JsonDocument.Parse(typesBody);
        var sessionTypeId = typesDoc.RootElement.EnumerateArray().First().GetProperty("id").GetString();

        // Create booking as customer
        var customer = await CreateCustomerClientAsync();
        var bookingResponse = await customer.PostAsJsonAsync("/api/booking/bookings", new
        {
            sessionTypeId, slotId,
            clientName = "Cancel Test", clientEmail = "cancel@example.com"
        });
        bookingResponse.EnsureSuccessStatusCode();
        var bookingBody = await bookingResponse.Content.ReadAsStringAsync();
        using var bookingDoc = JsonDocument.Parse(bookingBody);
        var bookingId = bookingDoc.RootElement.GetProperty("id").GetString();

        // Cancel
        var cancelResponse = await customer.PutAsync($"/api/booking/bookings/{bookingId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        var cancelBody = await cancelResponse.Content.ReadAsStringAsync();
        using var cancelDoc = JsonDocument.Parse(cancelBody);
        Assert.Equal("Cancelled", cancelDoc.RootElement.GetProperty("status").GetString());
    }

    // ── Email verification ────────────────────────────────────────

    [Fact]
    public async Task CreateBooking_SendsConfirmationEmails()
    {
        FakeEmail.Clear();

        var (slotId, sessionTypeId) = await CreateSlotAndGetSessionTypeAsync("2027-01-10");

        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/booking/bookings", new
        {
            sessionTypeId,
            slotId,
            clientName = "Email Test",
            clientEmail = "emailtest@example.com",
            dogCount = 1
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Should have sent exactly 2 emails: one to customer, one to photographer
        Assert.Equal(2, FakeEmail.SentEmails.Count);

        var customerEmail = FakeEmail.SentEmails.FirstOrDefault(e => e.To == "emailtest@example.com");
        Assert.NotNull(customerEmail);
        Assert.Contains("Booking Confirmation", customerEmail.Subject);
        Assert.Contains("Email Test", customerEmail.HtmlBody);

        var photographerEmail = FakeEmail.SentEmails.FirstOrDefault(e => e.To == "admin@partlphoto.sk");
        Assert.NotNull(photographerEmail);
        Assert.Contains("New Booking", photographerEmail.Subject);
        Assert.Contains("emailtest@example.com", photographerEmail.HtmlBody);
    }

    // ── Edge cases ────────────────────────────────────────────────

    [Fact]
    public async Task CreateBooking_BlockedSlot_Returns400()
    {
        var admin = await CreateAdminClientAsync();

        // Create and block a slot
        var slotResponse = await admin.PostAsJsonAsync("/api/booking/availability", new
        {
            date = "2027-02-10",
            startTime = "09:00",
            endTime = "10:30",
            isBlocked = true
        });
        slotResponse.EnsureSuccessStatusCode();
        var slotBody = await slotResponse.Content.ReadAsStringAsync();
        using var slotDoc = JsonDocument.Parse(slotBody);
        var slotId = slotDoc.RootElement.GetProperty("slots").EnumerateArray().First().GetProperty("id").GetString();

        var typesResponse = await CreateClient().GetAsync("/api/booking/session-types");
        var typesBody = await typesResponse.Content.ReadAsStringAsync();
        using var typesDoc = JsonDocument.Parse(typesBody);
        var sessionTypeId = typesDoc.RootElement.EnumerateArray().First().GetProperty("id").GetString();

        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/booking/bookings", new
        {
            sessionTypeId,
            slotId,
            clientName = "Blocked Test",
            clientEmail = "blocked@example.com"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("blocked", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateBooking_DogCountExceedsMax_Returns400()
    {
        // dog-portrait session allows max 2 dogs
        var (slotId, _) = await CreateSlotAndGetSessionTypeAsync("2027-02-15");

        var client = CreateClient();
        var typesResponse = await client.GetAsync("/api/booking/session-types/dog-portrait");
        var typesBody = await typesResponse.Content.ReadAsStringAsync();
        using var typesDoc = JsonDocument.Parse(typesBody);
        var sessionTypeId = typesDoc.RootElement.GetProperty("id").GetString();
        var maxDogs = typesDoc.RootElement.GetProperty("maxDogs").GetInt32();

        var response = await client.PostAsJsonAsync("/api/booking/bookings", new
        {
            sessionTypeId,
            slotId,
            clientName = "Too Many Dogs",
            clientEmail = "dogs@example.com",
            dogCount = maxDogs + 1
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Dog count", body);
    }

    [Fact]
    public async Task BulkGeneration_WithBreaks_CreatesCorrectSlotCount()
    {
        var admin = await CreateAdminClientAsync();

        // 09:00-15:00, 60-min slots with 30-min breaks
        // Expected: 09:00-10:00, 10:30-11:30, 12:00-13:00, 13:30-14:30 = 4 slots
        // (15:00-15:00 boundary: 14:30+0:30=15:00, 15:00+1:00=16:00 > 15:00 → 4 slots)
        var response = await admin.PostAsJsonAsync("/api/booking/availability", new
        {
            date = "2027-03-14",
            startTime = "09:00",
            endTime = "15:00",
            slotDurationMinutes = 60,
            breakMinutes = 30
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var count = doc.RootElement.GetProperty("count").GetInt32();
        Assert.Equal(4, count);

        // Verify slot times
        var slots = doc.RootElement.GetProperty("slots").EnumerateArray().ToList();
        Assert.Equal("09:00", slots[0].GetProperty("startTime").GetString());
        Assert.Equal("10:00", slots[0].GetProperty("endTime").GetString());
        Assert.Equal("10:30", slots[1].GetProperty("startTime").GetString());
        Assert.Equal("11:30", slots[1].GetProperty("endTime").GetString());
        Assert.Equal("12:00", slots[2].GetProperty("startTime").GetString());
        Assert.Equal("13:00", slots[2].GetProperty("endTime").GetString());
        Assert.Equal("13:30", slots[3].GetProperty("startTime").GetString());
        Assert.Equal("14:30", slots[3].GetProperty("endTime").GetString());
    }

    [Fact]
    public async Task GetSessionTypes_LanguageParameter_ReturnsLocalizedNames()
    {
        var client = CreateClient();

        var skResponse = await client.GetAsync("/api/booking/session-types?lang=sk");
        var skBody = await skResponse.Content.ReadAsStringAsync();
        using var skDoc = JsonDocument.Parse(skBody);
        var skName = skDoc.RootElement.EnumerateArray().First().GetProperty("name").GetString();

        var enResponse = await client.GetAsync("/api/booking/session-types?lang=en");
        var enBody = await enResponse.Content.ReadAsStringAsync();
        using var enDoc = JsonDocument.Parse(enBody);
        var enName = enDoc.RootElement.EnumerateArray().First().GetProperty("name").GetString();

        Assert.NotEqual(skName, enName);
    }
}
