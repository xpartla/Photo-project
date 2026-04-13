using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DogPhoto.IntegrationTests;

public sealed class BookingTests : ApiTestBase
{
    public BookingTests(ApiFactory factory) : base(factory) { }

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
}
