using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DogPhoto.Infrastructure.Email;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DogPhoto.IntegrationTests;

public sealed class BookingTests : ApiTestBase
{
    public BookingTests(ApiFactory factory) : base(factory) { }

    // The literals passed to D() are template dates. They are shifted by a
    // whole number of weeks so they always land in the future (the API rejects
    // bookings on past slots) while keeping their weekday and relative spacing.
    private static readonly DateOnly TemplateAnchor = new(2026, 6, 1);
    private static readonly DateOnly ShiftedAnchor = NextMonday(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30));

    private static DateOnly NextMonday(DateOnly from) =>
        from.AddDays(((int)DayOfWeek.Monday - (int)from.DayOfWeek + 7) % 7);

    private static string D(string templateDate)
    {
        var template = DateOnly.ParseExact(templateDate, "yyyy-MM-dd");
        return ShiftedAnchor.AddDays(template.DayNumber - TemplateAnchor.DayNumber).ToString("yyyy-MM-dd");
    }

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
        var response = await client.GetAsync("/api/booking/session-types/legacy");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("legacy", doc.RootElement.GetProperty("slug").GetString());
        Assert.Equal(90, doc.RootElement.GetProperty("durationMinutes").GetInt32());
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
            date = D("2026-06-20"),
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
            date = D("2026-07-11"),
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
        var date = D("2026-08-15");
        await admin.PostAsJsonAsync("/api/booking/availability", new
        {
            date,
            startTime = "10:00",
            endTime = "12:00"
        });

        var parsed = DateOnly.ParseExact(date, "yyyy-MM-dd");
        var client = CreateClient();
        var response = await client.GetAsync($"/api/booking/availability?month={parsed.Month}&year={parsed.Year}");

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
            date = D("2026-09-05"),
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
            date = D("2026-09-12"),
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
    public async Task AdminCanBlockSlot()
    {
        var admin = await CreateAdminClientAsync();

        // Create a slot
        var slotResponse = await admin.PostAsJsonAsync("/api/booking/availability", new
        {
            date = D("2026-10-03"),
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
            date = D("2026-11-01"),
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
            date = D("2026-09-20"),
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

    private async Task<(string BookingId, string Recipient)> BookAsync(
        HttpClient client, string templateDate, string? language = null,
        string clientName = "Email Test", string? specialRequests = null)
    {
        var (slotId, sessionTypeId) = await CreateSlotAndGetSessionTypeAsync(D(templateDate));
        var recipient = $"booking-{Guid.NewGuid():N}@example.test";

        var response = await client.PostAsJsonAsync("/api/booking/bookings", new
        {
            sessionTypeId,
            slotId,
            clientName,
            clientEmail = recipient,
            dogCount = 1,
            specialRequests,
            language
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (doc.RootElement.GetProperty("id").GetString()!, recipient);
    }

    private FakeEmailService.SentEmail[] EmailsTo(string recipient) =>
        FakeEmail.SentEmails.Where(e => e.To == recipient).ToArray();

    [Fact]
    public async Task CreateBooking_WithoutLanguage_SendsSlovakCustomerEmail_AndEnglishPhotographerEmail()
    {
        var (_, recipient) = await BookAsync(CreateClient(), "2027-01-10");

        var customerEmail = Assert.Single(EmailsTo(recipient));
        Assert.Contains("Potvrdenie rezervácie", customerEmail.Subject);
        Assert.Contains("Dobrý deň", customerEmail.HtmlBody);
        Assert.Contains("Email Test", customerEmail.HtmlBody);
        Assert.DoesNotContain("Thank you for your booking", customerEmail.HtmlBody);

        var photographerEmail = Assert.Single(
            EmailsTo("admin@partlphoto.sk"), e => e.HtmlBody.Contains(recipient));
        Assert.Contains("New Booking", photographerEmail.Subject);
    }

    [Fact]
    public async Task CreateBooking_InEnglish_SendsEnglishCustomerEmail()
    {
        var (_, recipient) = await BookAsync(CreateClient(), "2027-01-11", language: "en");

        var customerEmail = Assert.Single(EmailsTo(recipient));
        Assert.Contains("Booking Confirmation", customerEmail.Subject);
        Assert.Contains("Thank you for your booking", customerEmail.HtmlBody);
        Assert.DoesNotContain("Dobrý deň", customerEmail.HtmlBody);
    }

    [Fact]
    public async Task CreateBooking_UnknownLanguage_FallsBackToSlovak()
    {
        var (_, recipient) = await BookAsync(CreateClient(), "2027-01-12", language: "de");

        Assert.Contains("Potvrdenie rezervácie", Assert.Single(EmailsTo(recipient)).Subject);
    }

    [Fact]
    public async Task ConfirmAndCancel_EmailTheCustomerInTheBookingLanguage()
    {
        var admin = await CreateAdminClientAsync();

        var (englishId, english) = await BookAsync(CreateClient(), "2027-01-13", language: "en");
        (await admin.PutAsync($"/api/booking/bookings/{englishId}/confirm", null)).EnsureSuccessStatusCode();
        Assert.Contains(EmailsTo(english), e => e.Subject.Contains("Your booking is confirmed"));

        var (slovakId, slovak) = await BookAsync(CreateClient(), "2027-01-14");
        (await admin.PutAsync($"/api/booking/bookings/{slovakId}/confirm", null)).EnsureSuccessStatusCode();
        (await admin.PutAsync($"/api/booking/bookings/{slovakId}/cancel", null)).EnsureSuccessStatusCode();
        Assert.Contains(EmailsTo(slovak), e => e.Subject.Contains("Vaša rezervácia je potvrdená"));
        Assert.Contains(EmailsTo(slovak), e => e.Subject.Contains("Vaša rezervácia bola zrušená"));
    }

    [Fact]
    public async Task BookingEmails_EscapeHtmlFromTheBookingForm()
    {
        const string script = "<script>alert(1)</script>";
        var (_, recipient) = await BookAsync(CreateClient(), "2027-01-15",
            clientName: $"Eve {script}", specialRequests: "<img src=x onerror=alert(2)>");

        var customerEmail = Assert.Single(EmailsTo(recipient));
        var photographerEmail = Assert.Single(
            EmailsTo("admin@partlphoto.sk"), e => e.HtmlBody.Contains(recipient));

        foreach (var body in new[] { customerEmail.HtmlBody, photographerEmail.HtmlBody })
        {
            Assert.DoesNotContain(script, body);
            Assert.Contains("&lt;script&gt;", body);
        }
        Assert.DoesNotContain("<img src=x", photographerEmail.HtmlBody);
    }

    [Fact]
    public async Task PhotographerNotification_GoesToTheConfiguredAddress()
    {
        const string studio = "studio@example.test";
        using var host = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Configure<NotificationSettings>(settings => settings.PhotographerEmail = studio)));

        var (_, recipient) = await BookAsync(host.CreateClient(), "2027-01-16");

        Assert.Contains(EmailsTo(studio), e => e.HtmlBody.Contains(recipient));
        Assert.DoesNotContain(EmailsTo("admin@partlphoto.sk"), e => e.HtmlBody.Contains(recipient));
    }

    [Fact]
    public async Task CreateBooking_BlockedSlot_Returns400()
    {
        var admin = await CreateAdminClientAsync();

        // Create and block a slot
        var slotResponse = await admin.PostAsJsonAsync("/api/booking/availability", new
        {
            date = D("2027-02-10"),
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
        // action session allows max 2 dogs
        var (slotId, _) = await CreateSlotAndGetSessionTypeAsync(D("2027-02-15"));

        var client = CreateClient();
        var typesResponse = await client.GetAsync("/api/booking/session-types/action");
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
            date = D("2027-03-14"),
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

    // ── Recurring availability (Epic 8) ─────────────────────────────

    [Fact]
    public async Task RecurringAvailability_RequiresAdmin()
    {
        var customer = await CreateCustomerClientAsync();
        var resp = await customer.PostAsJsonAsync("/api/booking/availability/recurring", new
        {
            daysOfWeek = new[] { 6 },
            fromDate = D("2027-04-03"),
            toDate = D("2027-04-24"),
            startTime = "09:00",
            endTime = "12:00",
            slotDurationMinutes = 60
        });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task RecurringAvailability_GeneratesSlotsForMatchingWeekdays()
    {
        var admin = await CreateAdminClientAsync();
        // 2027-04-03 .. 2027-04-24 inclusive contains 4 Saturdays (3, 10, 17, 24).
        // 09:00–12:00 with 60-min slots and no break → 3 slots/day × 4 days = 12 slots.
        var resp = await admin.PostAsJsonAsync("/api/booking/availability/recurring", new
        {
            daysOfWeek = new[] { 6 }, // 6 = Saturday (matches DayOfWeek enum)
            fromDate = D("2027-04-03"),
            toDate = D("2027-04-24"),
            startTime = "09:00",
            endTime = "12:00",
            slotDurationMinutes = 60
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal(12, doc.RootElement.GetProperty("count").GetInt32());
        Assert.Equal(4, doc.RootElement.GetProperty("addedDays").GetInt32());
        Assert.Equal(0, doc.RootElement.GetProperty("skippedDays").GetInt32());
    }

    [Fact]
    public async Task RecurringAvailability_SkipsDaysThatAlreadyHaveSlots()
    {
        var admin = await CreateAdminClientAsync();
        // Pre-create a slot on the first Friday of the run; the recurring call
        // should skip that day entirely (whole-day skip, not per-slot).
        await admin.PostAsJsonAsync("/api/booking/availability", new
        {
            date = D("2027-05-07"),
            startTime = "10:00",
            endTime = "11:00"
        });

        // 2027-05-07 .. 2027-05-21 has 3 Fridays (7, 14, 21). One is pre-occupied → 2 days × 3 hrs/day @ 60-min = 6 slots.
        var resp = await admin.PostAsJsonAsync("/api/booking/availability/recurring", new
        {
            daysOfWeek = new[] { 5 }, // 5 = Friday
            fromDate = D("2027-05-07"),
            toDate = D("2027-05-21"),
            startTime = "09:00",
            endTime = "12:00",
            slotDurationMinutes = 60
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal(6, doc.RootElement.GetProperty("count").GetInt32());
        Assert.Equal(2, doc.RootElement.GetProperty("addedDays").GetInt32());
        Assert.Equal(1, doc.RootElement.GetProperty("skippedDays").GetInt32());
    }

    [Fact]
    public async Task RecurringAvailability_RejectsInvertedDateRange()
    {
        var admin = await CreateAdminClientAsync();
        var resp = await admin.PostAsJsonAsync("/api/booking/availability/recurring", new
        {
            daysOfWeek = new[] { 6 },
            fromDate = D("2027-06-30"),
            toDate = D("2027-06-01"),
            startTime = "09:00",
            endTime = "12:00",
            slotDurationMinutes = 60
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task RecurringAvailability_RejectsEmptyDayList()
    {
        var admin = await CreateAdminClientAsync();
        var resp = await admin.PostAsJsonAsync("/api/booking/availability/recurring", new
        {
            daysOfWeek = Array.Empty<int>(),
            fromDate = D("2027-07-01"),
            toDate = D("2027-07-31"),
            startTime = "09:00",
            endTime = "12:00",
            slotDurationMinutes = 60
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ── Admin-wide bookings endpoint ────────────────────────────────

    [Fact]
    public async Task AdminBookings_RequiresAdmin()
    {
        var customer = await CreateCustomerClientAsync();
        var resp = await customer.GetAsync("/api/booking/admin/bookings");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task AdminBookings_ListsAcrossUsers_AndFiltersByStatus()
    {
        var admin = await CreateAdminClientAsync();

        // Set up a session type + slot owned by the admin-created world.
        var sessionTypes = await admin.GetFromJsonAsync<JsonElement>("/api/booking/session-types");
        var sessionId = sessionTypes.EnumerateArray().First().GetProperty("id").GetString()!;

        var slotResp = await admin.PostAsJsonAsync("/api/booking/availability", new
        {
            date = D("2027-08-07"),
            startTime = "10:00",
            endTime = "11:00"
        });
        var slotId = (await slotResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("slots").EnumerateArray().First().GetProperty("id").GetString()!;

        // Customer makes a booking.
        var customer = await CreateCustomerClientAsync();
        var bookResp = await customer.PostAsJsonAsync("/api/booking/bookings", new
        {
            sessionTypeId = sessionId,
            slotId,
            clientName = "Customer A",
            clientEmail = "customer-a@example.test",
            dogCount = 1
        });
        bookResp.EnsureSuccessStatusCode();

        // Admin sees it in /admin/bookings.
        var all = await admin.GetFromJsonAsync<JsonElement>("/api/booking/admin/bookings");
        var emails = all.EnumerateArray().Select(b => b.GetProperty("clientEmail").GetString()!).ToList();
        Assert.Contains("customer-a@example.test", emails);

        // Filtered to Cancelled → not present (still Pending).
        var cancelled = await admin.GetFromJsonAsync<JsonElement>("/api/booking/admin/bookings?status=Cancelled");
        var cancelledEmails = cancelled.EnumerateArray().Select(b => b.GetProperty("clientEmail").GetString()!).ToList();
        Assert.DoesNotContain("customer-a@example.test", cancelledEmails);
    }

    [Fact]
    public async Task GetSessionTypes_LanguageParameter_ReturnsLocalizedDescriptions()
    {
        // The seeded packages share one name in both languages, so the
        // description is the field that proves localization.
        var client = CreateClient();

        var skResponse = await client.GetAsync("/api/booking/session-types?lang=sk");
        var skBody = await skResponse.Content.ReadAsStringAsync();
        using var skDoc = JsonDocument.Parse(skBody);
        var skDescription = skDoc.RootElement.EnumerateArray().First().GetProperty("description").GetString();

        var enResponse = await client.GetAsync("/api/booking/session-types?lang=en");
        var enBody = await enResponse.Content.ReadAsStringAsync();
        using var enDoc = JsonDocument.Parse(enBody);
        var enDescription = enDoc.RootElement.EnumerateArray().First().GetProperty("description").GetString();

        Assert.NotEqual(skDescription, enDescription);
    }
}
