using System.Text.Json;
using DogPhoto.Infrastructure.Email;
using DogPhoto.Infrastructure.Persistence.Booking;
using DogPhoto.SharedKernel.Auth;
using DogPhoto.SharedKernel.Email;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DogPhoto.Infrastructure.Booking;

public static class BookingEndpoints
{
    public static void MapBookingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/booking").WithTags("Booking");

        // ── Public endpoints ───────────────────────────────────────────

        group.MapGet("/session-types", async (BookingDbContext db, string? lang) =>
        {
            var types = await db.SessionTypes
                .Where(st => st.IsActive)
                .OrderBy(st => st.BasePrice)
                .ToListAsync();

            var l = lang ?? "sk";
            return Results.Ok(types.Select(st => MapSessionType(st, l)));
        });

        group.MapGet("/session-types/{slug}", async (string slug, BookingDbContext db, string? lang) =>
        {
            var st = await db.SessionTypes.FirstOrDefaultAsync(s => s.Slug == slug && s.IsActive);
            if (st is null) return Results.NotFound();

            var l = lang ?? "sk";
            return Results.Ok(MapSessionType(st, l));
        });

        group.MapGet("/availability", async (BookingDbContext db, int? month, int? year) =>
        {
            var now = DateTime.UtcNow;
            var targetMonth = month ?? now.Month;
            var targetYear = year ?? now.Year;

            var startDate = new DateOnly(targetYear, targetMonth, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            // Get all slots for the month
            var slots = await db.AvailabilitySlots
                .Where(s => s.Date >= startDate && s.Date <= endDate)
                .OrderBy(s => s.Date)
                .ThenBy(s => s.StartTime)
                .ToListAsync();

            // Get booked slot IDs for this period
            var bookedSlotIds = await db.Bookings
                .Where(b => b.SlotId.HasValue && (b.Status == "Pending" || b.Status == "Confirmed"))
                .Where(b => b.Slot!.Date >= startDate && b.Slot!.Date <= endDate)
                .Select(b => b.SlotId!.Value)
                .ToListAsync();

            var today = DateOnly.FromDateTime(now);
            var currentTime = TimeOnly.FromDateTime(now);

            return Results.Ok(slots.Select(s => new
            {
                id = s.Id,
                date = s.Date.ToString("yyyy-MM-dd"),
                startTime = s.StartTime.ToString("HH:mm"),
                endTime = s.EndTime.ToString("HH:mm"),
                isBlocked = s.IsBlocked,
                isBooked = bookedSlotIds.Contains(s.Id),
                isPast = s.Date < today || (s.Date == today && s.EndTime <= currentTime)
            }));
        });

        group.MapPost("/bookings", async (
            CreateBookingRequest request,
            BookingDbContext db,
            ICurrentUser currentUser,
            IEmailService emailService,
            ILoggerFactory loggerFactory) =>
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(request.ClientName))
                return Results.BadRequest(new { error = "Client name is required." });
            if (string.IsNullOrWhiteSpace(request.ClientEmail))
                return Results.BadRequest(new { error = "Client email is required." });

            // Validate session type
            var sessionType = await db.SessionTypes.FirstOrDefaultAsync(st => st.Id == request.SessionTypeId && st.IsActive);
            if (sessionType is null)
                return Results.BadRequest(new { error = "Invalid session type." });

            // Validate dog count
            if (request.DogCount < 1 || request.DogCount > sessionType.MaxDogs)
                return Results.BadRequest(new { error = $"Dog count must be between 1 and {sessionType.MaxDogs}." });

            // Validate slot if provided
            AvailabilitySlot? slot = null;
            if (request.SlotId.HasValue)
            {
                slot = await db.AvailabilitySlots.FirstOrDefaultAsync(s => s.Id == request.SlotId.Value);
                if (slot is null)
                    return Results.BadRequest(new { error = "Invalid availability slot." });
                if (slot.IsBlocked)
                    return Results.BadRequest(new { error = "This slot is blocked." });

                // Check if slot is in the past
                var now = DateTime.UtcNow;
                var today = DateOnly.FromDateTime(now);
                if (slot.Date < today || (slot.Date == today && slot.EndTime <= TimeOnly.FromDateTime(now)))
                    return Results.BadRequest(new { error = "Cannot book a slot in the past." });

                // Check if slot is already booked
                var alreadyBooked = await db.Bookings
                    .AnyAsync(b => b.SlotId == slot.Id && (b.Status == "Pending" || b.Status == "Confirmed"));
                if (alreadyBooked)
                    return Results.Conflict(new { error = "This slot is already booked." });
            }

            var booking = new BookingEntity
            {
                UserId = currentUser.IsAuthenticated ? currentUser.UserId : null,
                SessionTypeId = sessionType.Id,
                SlotId = slot?.Id,
                Status = "Pending",
                ClientName = request.ClientName,
                ClientEmail = request.ClientEmail,
                ClientPhone = request.ClientPhone,
                DogCount = request.DogCount,
                SpecialRequests = request.SpecialRequests,
                TotalPrice = sessionType.BasePrice
            };

            db.Bookings.Add(booking);
            await db.SaveChangesAsync();

            // Send confirmation emails
            try
            {
                var dateStr = slot?.Date.ToString("dd.MM.yyyy") ?? "TBD";
                var timeStr = slot is not null ? $"{slot.StartTime:HH:mm} - {slot.EndTime:HH:mm}" : "TBD";

                var customerHtml = BookingEmailTemplates.CustomerConfirmation(
                    booking.ClientName,
                    sessionType.NameEn,
                    dateStr,
                    timeStr,
                    booking.TotalPrice,
                    sessionType.Currency);

                await emailService.SendAsync(
                    booking.ClientEmail,
                    "Booking Confirmation — PartlPhoto",
                    customerHtml);

                var photographerHtml = BookingEmailTemplates.PhotographerNotification(
                    booking.ClientName,
                    booking.ClientEmail,
                    booking.ClientPhone,
                    sessionType.NameEn,
                    dateStr,
                    timeStr,
                    booking.DogCount,
                    booking.SpecialRequests);

                await emailService.SendAsync(
                    "admin@partlphoto.sk",
                    $"New Booking: {booking.ClientName} — {sessionType.NameEn}",
                    photographerHtml);
            }
            catch (Exception ex)
            {
                loggerFactory.CreateLogger("Booking").LogError(ex, "Failed to send booking confirmation emails for booking {BookingId}", booking.Id);
            }

            return Results.Created($"/api/booking/bookings/{booking.Id}", new
            {
                id = booking.Id,
                status = booking.Status,
                sessionType = new { id = sessionType.Id, name = sessionType.NameEn, slug = sessionType.Slug },
                slot = slot is not null ? new { date = slot.Date.ToString("yyyy-MM-dd"), startTime = slot.StartTime.ToString("HH:mm"), endTime = slot.EndTime.ToString("HH:mm") } : null,
                clientName = booking.ClientName,
                clientEmail = booking.ClientEmail,
                totalPrice = booking.TotalPrice,
                currency = sessionType.Currency
            });
        });

        // ── Authenticated endpoints ────────────────────────────────────

        var auth = app.MapGroup("/api/booking").WithTags("Booking").RequireAuthorization();

        auth.MapGet("/bookings/{id:guid}", async (Guid id, BookingDbContext db, ICurrentUser currentUser) =>
        {
            var booking = await db.Bookings
                .Include(b => b.SessionType)
                .Include(b => b.Slot)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking is null) return Results.NotFound();

            // Only the owner or admin can view
            if (!currentUser.IsAdmin && booking.UserId != currentUser.UserId)
                return Results.Forbid();

            return Results.Ok(MapBookingDetail(booking));
        });

        auth.MapPut("/bookings/{id:guid}/cancel", async (
            Guid id,
            BookingDbContext db,
            ICurrentUser currentUser,
            IEmailService emailService,
            ILoggerFactory loggerFactory) =>
        {
            var booking = await db.Bookings
                .Include(b => b.SessionType)
                .Include(b => b.Slot)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking is null) return Results.NotFound();

            if (!currentUser.IsAdmin && booking.UserId != currentUser.UserId)
                return Results.Forbid();

            if (booking.Status == "Cancelled")
                return Results.BadRequest(new { error = "Booking is already cancelled." });

            booking.Status = "Cancelled";
            booking.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            await TrySendStatusEmailAsync(booking, emailService, loggerFactory,
                isConfirmation: false);

            return Results.Ok(new { id = booking.Id, status = booking.Status });
        });

        auth.MapPut("/bookings/{id:guid}/confirm", async (
            Guid id,
            BookingDbContext db,
            ICurrentUser currentUser,
            IEmailService emailService,
            ILoggerFactory loggerFactory) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();

            var booking = await db.Bookings
                .Include(b => b.SessionType)
                .Include(b => b.Slot)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking is null) return Results.NotFound();

            if (booking.Status == "Confirmed")
                return Results.BadRequest(new { error = "Booking is already confirmed." });
            if (booking.Status == "Cancelled")
                return Results.BadRequest(new { error = "Cannot confirm a cancelled booking." });

            booking.Status = "Confirmed";
            booking.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            await TrySendStatusEmailAsync(booking, emailService, loggerFactory,
                isConfirmation: true);

            return Results.Ok(new { id = booking.Id, status = booking.Status });
        });

        auth.MapGet("/my-bookings", async (BookingDbContext db, ICurrentUser currentUser) =>
        {
            var bookings = await db.Bookings
                .Where(b => b.UserId == currentUser.UserId)
                .Include(b => b.SessionType)
                .Include(b => b.Slot)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return Results.Ok(bookings.Select(MapBookingDetail));
        });

        // Admin-wide bookings list (includes anonymous bookings + every user's bookings).
        auth.MapGet("/admin/bookings", async (BookingDbContext db, ICurrentUser currentUser, string? status) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();

            var query = db.Bookings.Include(b => b.SessionType).Include(b => b.Slot).AsQueryable();
            if (!string.IsNullOrEmpty(status))
                query = query.Where(b => b.Status == status);

            var bookings = await query.OrderByDescending(b => b.CreatedAt).Take(200).ToListAsync();
            return Results.Ok(bookings.Select(MapBookingDetail));
        });

        // ── Admin endpoints ────────────────────────────────────────────

        auth.MapPost("/availability", async (
            CreateAvailabilityRequest request,
            ICurrentUser currentUser,
            BookingDbContext db) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();

            var slots = new List<AvailabilitySlot>();

            if (request.SlotDurationMinutes.HasValue && request.SlotDurationMinutes > 0)
            {
                // Bulk generation: expand date + time range into individual slots
                var startTime = TimeOnly.Parse(request.StartTime);
                var endTime = TimeOnly.Parse(request.EndTime);
                var duration = TimeSpan.FromMinutes(request.SlotDurationMinutes.Value);
                var breakTime = TimeSpan.FromMinutes(request.BreakMinutes ?? 0);

                var current = startTime;
                while (current.Add(duration) <= endTime)
                {
                    slots.Add(new AvailabilitySlot
                    {
                        Date = DateOnly.Parse(request.Date),
                        StartTime = current,
                        EndTime = current.Add(duration),
                        IsBlocked = false
                    });
                    current = current.Add(duration).Add(breakTime);
                }
            }
            else
            {
                // Single slot
                slots.Add(new AvailabilitySlot
                {
                    Date = DateOnly.Parse(request.Date),
                    StartTime = TimeOnly.Parse(request.StartTime),
                    EndTime = TimeOnly.Parse(request.EndTime),
                    IsBlocked = request.IsBlocked ?? false
                });
            }

            if (slots.Count == 0)
                return Results.BadRequest(new { error = "No slots could be generated from the given time range." });

            db.AvailabilitySlots.AddRange(slots);
            await db.SaveChangesAsync();

            return Results.Created("/api/booking/availability", new
            {
                count = slots.Count,
                slots = slots.Select(s => new
                {
                    id = s.Id,
                    date = s.Date.ToString("yyyy-MM-dd"),
                    startTime = s.StartTime.ToString("HH:mm"),
                    endTime = s.EndTime.ToString("HH:mm")
                })
            });
        });

        auth.MapPost("/availability/recurring", async (
            RecurringAvailabilityRequest request,
            ICurrentUser currentUser,
            BookingDbContext db) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();

            if (request.DaysOfWeek is null || request.DaysOfWeek.Count == 0)
                return Results.BadRequest(new { error = "At least one day of week is required." });

            DateOnly fromDate, toDate;
            TimeOnly startTime, endTime;
            try
            {
                fromDate = DateOnly.Parse(request.FromDate);
                toDate = DateOnly.Parse(request.ToDate);
                startTime = TimeOnly.Parse(request.StartTime);
                endTime = TimeOnly.Parse(request.EndTime);
            }
            catch (FormatException)
            {
                return Results.BadRequest(new { error = "Invalid date or time format." });
            }

            if (toDate < fromDate)
                return Results.BadRequest(new { error = "toDate must be on or after fromDate." });
            if (endTime <= startTime)
                return Results.BadRequest(new { error = "endTime must be after startTime." });
            if (request.SlotDurationMinutes <= 0)
                return Results.BadRequest(new { error = "slotDurationMinutes must be positive." });

            var dayHashSet = request.DaysOfWeek.Select(d => (DayOfWeek)d).ToHashSet();
            var duration = TimeSpan.FromMinutes(request.SlotDurationMinutes);
            var breakTime = TimeSpan.FromMinutes(request.BreakMinutes ?? 0);

            // Pre-load existing slot dates in range so we can skip days that already
            // have any slots — avoids accidentally double-booking a previously edited day.
            var existingDates = await db.AvailabilitySlots
                .Where(s => s.Date >= fromDate && s.Date <= toDate)
                .Select(s => s.Date)
                .Distinct()
                .ToListAsync();
            var skipDates = existingDates.ToHashSet();

            var newSlots = new List<AvailabilitySlot>();
            var skippedDays = 0;
            var addedDays = 0;

            for (var date = fromDate; date <= toDate; date = date.AddDays(1))
            {
                if (!dayHashSet.Contains(date.DayOfWeek)) continue;
                if (skipDates.Contains(date)) { skippedDays++; continue; }

                var anyAddedForThisDay = false;
                var current = startTime;
                while (current.Add(duration) <= endTime)
                {
                    newSlots.Add(new AvailabilitySlot
                    {
                        Date = date,
                        StartTime = current,
                        EndTime = current.Add(duration),
                        IsBlocked = false
                    });
                    anyAddedForThisDay = true;
                    current = current.Add(duration).Add(breakTime);
                }
                if (anyAddedForThisDay) addedDays++;
            }

            if (newSlots.Count == 0)
                return Results.Ok(new { count = 0, addedDays = 0, skippedDays, message = "No new slots generated." });

            db.AvailabilitySlots.AddRange(newSlots);
            await db.SaveChangesAsync();

            return Results.Created("/api/booking/availability", new
            {
                count = newSlots.Count,
                addedDays,
                skippedDays
            });
        });

        auth.MapPut("/availability/{id:guid}", async (
            Guid id,
            UpdateAvailabilityRequest request,
            ICurrentUser currentUser,
            BookingDbContext db) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();

            var slot = await db.AvailabilitySlots.FindAsync(id);
            if (slot is null) return Results.NotFound();

            if (request.StartTime is not null) slot.StartTime = TimeOnly.Parse(request.StartTime);
            if (request.EndTime is not null) slot.EndTime = TimeOnly.Parse(request.EndTime);
            if (request.IsBlocked.HasValue) slot.IsBlocked = request.IsBlocked.Value;

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                id = slot.Id,
                date = slot.Date.ToString("yyyy-MM-dd"),
                startTime = slot.StartTime.ToString("HH:mm"),
                endTime = slot.EndTime.ToString("HH:mm"),
                isBlocked = slot.IsBlocked
            });
        });

        auth.MapPost("/session-types", async (
            CreateSessionTypeRequest request,
            ICurrentUser currentUser,
            BookingDbContext db) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();

            if (string.IsNullOrWhiteSpace(request.Slug)) return Results.BadRequest(new { error = "Slug is required." });
            if (string.IsNullOrWhiteSpace(request.NameSk)) return Results.BadRequest(new { error = "NameSk is required." });
            if (string.IsNullOrWhiteSpace(request.NameEn)) return Results.BadRequest(new { error = "NameEn is required." });
            if (await db.SessionTypes.AnyAsync(s => s.Slug == request.Slug))
                return Results.Conflict(new { error = $"Session type with slug '{request.Slug}' already exists." });

            var st = new SessionType
            {
                Slug = request.Slug,
                NameSk = request.NameSk,
                NameEn = request.NameEn,
                DescriptionSk = request.DescriptionSk,
                DescriptionEn = request.DescriptionEn,
                DurationMinutes = request.DurationMinutes ?? 60,
                BasePrice = request.BasePrice ?? 100m,
                Category = request.Category ?? "portrait",
                MaxDogs = request.MaxDogs ?? 1,
                Currency = request.Currency ?? "EUR",
                IsActive = request.IsActive ?? true,
                IncludesJson = request.IncludesJson is null ? null : JsonSerializer.Serialize(request.IncludesJson),
            };
            db.SessionTypes.Add(st);
            await db.SaveChangesAsync();
            return Results.Created($"/api/booking/session-types/{st.Slug}", MapSessionType(st, "en"));
        });

        auth.MapPut("/session-types/{id:guid}", async (
            Guid id,
            UpdateSessionTypeRequest request,
            ICurrentUser currentUser,
            BookingDbContext db) =>
        {
            if (!currentUser.IsAdmin) return Results.Forbid();

            var st = await db.SessionTypes.FindAsync(id);
            if (st is null) return Results.NotFound();

            if (request.NameSk is not null) st.NameSk = request.NameSk;
            if (request.NameEn is not null) st.NameEn = request.NameEn;
            if (request.DescriptionSk is not null) st.DescriptionSk = request.DescriptionSk;
            if (request.DescriptionEn is not null) st.DescriptionEn = request.DescriptionEn;
            if (request.DurationMinutes.HasValue) st.DurationMinutes = request.DurationMinutes.Value;
            if (request.BasePrice.HasValue) st.BasePrice = request.BasePrice.Value;
            if (request.Category is not null) st.Category = request.Category;
            if (request.IncludesJson is not null) st.IncludesJson = JsonSerializer.Serialize(request.IncludesJson);
            if (request.MaxDogs.HasValue) st.MaxDogs = request.MaxDogs.Value;
            if (request.IsActive.HasValue) st.IsActive = request.IsActive.Value;
            st.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return Results.Ok(MapSessionType(st, "en"));
        });
    }

    private static async Task TrySendStatusEmailAsync(
        BookingEntity booking,
        IEmailService emailService,
        ILoggerFactory loggerFactory,
        bool isConfirmation)
    {
        if (string.IsNullOrWhiteSpace(booking.ClientEmail)) return;

        try
        {
            var dateStr = booking.Slot?.Date.ToString("dd.MM.yyyy") ?? "TBD";
            var timeStr = booking.Slot is not null
                ? $"{booking.Slot.StartTime:HH:mm} - {booking.Slot.EndTime:HH:mm}"
                : "TBD";

            string subject;
            string html;
            if (isConfirmation)
            {
                subject = "Your booking is confirmed — PartlPhoto";
                html = BookingEmailTemplates.CustomerConfirmed(
                    booking.ClientName,
                    booking.SessionType.NameEn,
                    dateStr,
                    timeStr,
                    booking.TotalPrice,
                    booking.SessionType.Currency);
            }
            else
            {
                subject = "Your booking was cancelled — PartlPhoto";
                html = BookingEmailTemplates.CustomerCancelled(
                    booking.ClientName,
                    booking.SessionType.NameEn,
                    dateStr,
                    timeStr);
            }

            await emailService.SendAsync(booking.ClientEmail, subject, html);
        }
        catch (Exception ex)
        {
            loggerFactory.CreateLogger("Booking").LogError(ex,
                "Failed to send booking status email for booking {BookingId} (confirmation={IsConfirmation})",
                booking.Id, isConfirmation);
        }
    }

    private static object MapSessionType(SessionType st, string lang)
    {
        string[]? includes = null;
        if (!string.IsNullOrEmpty(st.IncludesJson))
        {
            try { includes = JsonSerializer.Deserialize<string[]>(st.IncludesJson); }
            catch { /* ignore malformed JSON */ }
        }

        return new
        {
            id = st.Id,
            slug = st.Slug,
            name = lang == "en" ? st.NameEn : st.NameSk,
            nameSk = st.NameSk,
            nameEn = st.NameEn,
            description = lang == "en" ? st.DescriptionEn ?? st.DescriptionSk : st.DescriptionSk ?? st.DescriptionEn,
            descriptionSk = st.DescriptionSk,
            descriptionEn = st.DescriptionEn,
            durationMinutes = st.DurationMinutes,
            basePrice = st.BasePrice,
            currency = st.Currency,
            category = st.Category,
            includes,
            maxDogs = st.MaxDogs,
            isActive = st.IsActive
        };
    }

    private static object MapBookingDetail(BookingEntity b)
    {
        return new
        {
            id = b.Id,
            status = b.Status,
            sessionType = new
            {
                id = b.SessionType.Id,
                name = b.SessionType.NameEn,
                slug = b.SessionType.Slug
            },
            slot = b.Slot is not null ? new
            {
                date = b.Slot.Date.ToString("yyyy-MM-dd"),
                startTime = b.Slot.StartTime.ToString("HH:mm"),
                endTime = b.Slot.EndTime.ToString("HH:mm")
            } : null,
            clientName = b.ClientName,
            clientEmail = b.ClientEmail,
            clientPhone = b.ClientPhone,
            dogCount = b.DogCount,
            specialRequests = b.SpecialRequests,
            totalPrice = b.TotalPrice,
            depositPaid = b.DepositPaid,
            createdAt = b.CreatedAt
        };
    }
}

// ── Request DTOs ───────────────────────────────────────────────────

public record CreateBookingRequest(
    Guid SessionTypeId,
    Guid? SlotId,
    string ClientName,
    string ClientEmail,
    string? ClientPhone,
    int DogCount = 1,
    string? SpecialRequests = null);

public record CreateAvailabilityRequest(
    string Date,
    string StartTime,
    string EndTime,
    int? SlotDurationMinutes = null,
    int? BreakMinutes = null,
    bool? IsBlocked = null);

public record UpdateAvailabilityRequest(
    string? StartTime = null,
    string? EndTime = null,
    bool? IsBlocked = null);

public record RecurringAvailabilityRequest(
    List<int> DaysOfWeek,        // 0 = Sunday … 6 = Saturday (matches DayOfWeek enum)
    string FromDate,             // yyyy-MM-dd inclusive
    string ToDate,               // yyyy-MM-dd inclusive
    string StartTime,            // HH:mm
    string EndTime,              // HH:mm
    int SlotDurationMinutes,
    int? BreakMinutes = null);

public record UpdateSessionTypeRequest(
    string? NameSk = null,
    string? NameEn = null,
    string? DescriptionSk = null,
    string? DescriptionEn = null,
    int? DurationMinutes = null,
    decimal? BasePrice = null,
    string? Category = null,
    List<string>? IncludesJson = null,
    int? MaxDogs = null,
    bool? IsActive = null);

public record CreateSessionTypeRequest(
    string Slug,
    string NameSk,
    string NameEn,
    string? DescriptionSk = null,
    string? DescriptionEn = null,
    int? DurationMinutes = null,
    decimal? BasePrice = null,
    string? Category = null,
    List<string>? IncludesJson = null,
    int? MaxDogs = null,
    string? Currency = null,
    bool? IsActive = null);
