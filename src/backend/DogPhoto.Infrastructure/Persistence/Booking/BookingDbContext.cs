using Microsoft.EntityFrameworkCore;

namespace DogPhoto.Infrastructure.Persistence.Booking;

public class BookingDbContext(DbContextOptions<BookingDbContext> options) : DbContext(options)
{
    public const string Schema = "booking";

    public DbSet<SessionType> SessionTypes => Set<SessionType>();
    public DbSet<AvailabilitySlot> AvailabilitySlots => Set<AvailabilitySlot>();
    public DbSet<BookingEntity> Bookings => Set<BookingEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<SessionType>(entity =>
        {
            entity.ToTable("session_types");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.NameSk).IsRequired().HasMaxLength(256);
            entity.Property(e => e.NameEn).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.Property(e => e.BasePrice).HasPrecision(10, 2);
            entity.Property(e => e.Currency).IsRequired().HasMaxLength(3).HasDefaultValue("EUR");
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.IncludesJson).HasColumnType("jsonb");
        });

        modelBuilder.Entity<AvailabilitySlot>(entity =>
        {
            entity.ToTable("availability_slots");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
        });

        modelBuilder.Entity<BookingEntity>(entity =>
        {
            entity.ToTable("bookings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Pending");
            entity.Property(e => e.ClientName).IsRequired().HasMaxLength(256);
            entity.Property(e => e.ClientEmail).IsRequired().HasMaxLength(256);
            entity.Property(e => e.ClientPhone).HasMaxLength(50);
            entity.Property(e => e.TotalPrice).HasPrecision(10, 2);
            entity.Property(e => e.GoPayPaymentId).HasMaxLength(256);
            entity.HasOne(e => e.SessionType).WithMany().HasForeignKey(e => e.SessionTypeId);
            entity.HasOne(e => e.Slot).WithMany().HasForeignKey(e => e.SlotId);
        });
    }
}

public class SessionType
{
    public Guid Id { get; set; }
    public string NameSk { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? DescriptionSk { get; set; }
    public string? DescriptionEn { get; set; }
    public int DurationMinutes { get; set; }
    public decimal BasePrice { get; set; }
    public string Currency { get; set; } = "EUR";
    public string? Category { get; set; }
    public string? IncludesJson { get; set; }
    public int MaxDogs { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class AvailabilitySlot
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsBlocked { get; set; }
}

public class BookingEntity
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public Guid SessionTypeId { get; set; }
    public SessionType SessionType { get; set; } = default!;
    public Guid? SlotId { get; set; }
    public AvailabilitySlot? Slot { get; set; }
    public string Status { get; set; } = "Pending";
    public string ClientName { get; set; } = default!;
    public string ClientEmail { get; set; } = default!;
    public string? ClientPhone { get; set; }
    public int DogCount { get; set; } = 1;
    public string? SpecialRequests { get; set; }
    public decimal TotalPrice { get; set; }
    public bool DepositPaid { get; set; }
    public string? GoPayPaymentId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
