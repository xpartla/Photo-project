using Microsoft.EntityFrameworkCore;

namespace DogPhoto.Infrastructure.Persistence.Portfolio;

public class PortfolioDbContext(DbContextOptions<PortfolioDbContext> options) : DbContext(options)
{
    public const string Schema = "portfolio";

    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<PhotoVariant> PhotoVariants => Set<PhotoVariant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Photo>(entity =>
        {
            entity.ToTable("photos");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.TitleSk).HasMaxLength(256);
            entity.Property(e => e.TitleEn).HasMaxLength(256);
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.Property(e => e.AltTextSk).HasMaxLength(512);
            entity.Property(e => e.AltTextEn).HasMaxLength(512);
            entity.Property(e => e.CameraSettings).HasMaxLength(256);
            entity.Property(e => e.Location).HasMaxLength(256);
            entity.Property(e => e.DominantColor).HasMaxLength(7);
            entity.Property(e => e.Blurhash).HasMaxLength(64);
            entity.Property(e => e.OriginalBlobUrl).HasMaxLength(1024);
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.ToTable("tags");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.NameSk).IsRequired().HasMaxLength(128);
            entity.Property(e => e.NameEn).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(128);
            entity.HasIndex(e => e.Slug).IsUnique();
        });

        modelBuilder.Entity<PhotoTag>(entity =>
        {
            entity.ToTable("photo_tags");
            entity.HasKey(e => new { e.PhotoId, e.TagId });
            entity.HasOne(e => e.Photo).WithMany(p => p.PhotoTags).HasForeignKey(e => e.PhotoId);
            entity.HasOne(e => e.Tag).WithMany(t => t.PhotoTags).HasForeignKey(e => e.TagId);
        });

        modelBuilder.Entity<Collection>(entity =>
        {
            entity.ToTable("collections");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.NameSk).IsRequired().HasMaxLength(256);
            entity.Property(e => e.NameEn).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        modelBuilder.Entity<CollectionPhoto>(entity =>
        {
            entity.ToTable("collection_photos");
            entity.HasKey(e => new { e.CollectionId, e.PhotoId });
            entity.HasOne(e => e.Collection).WithMany(c => c.CollectionPhotos).HasForeignKey(e => e.CollectionId);
            entity.HasOne(e => e.Photo).WithMany(p => p.CollectionPhotos).HasForeignKey(e => e.PhotoId);
        });

        modelBuilder.Entity<PhotoVariant>(entity =>
        {
            entity.ToTable("photo_variants");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Format).IsRequired().HasMaxLength(10);
            entity.Property(e => e.BlobUrl).IsRequired().HasMaxLength(1024);
            entity.HasOne(e => e.Photo).WithMany(p => p.Variants).HasForeignKey(e => e.PhotoId);
        });
    }
}

public class Photo
{
    public Guid Id { get; set; }
    public string? TitleSk { get; set; }
    public string? TitleEn { get; set; }
    public string Slug { get; set; } = default!;
    public string? DescriptionSk { get; set; }
    public string? DescriptionEn { get; set; }
    public string? AltTextSk { get; set; }
    public string? AltTextEn { get; set; }
    public string? CameraSettings { get; set; }
    public string? Location { get; set; }
    public DateTime? ShotDate { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? DominantColor { get; set; }
    public string? Blurhash { get; set; }
    public string? OriginalBlobUrl { get; set; }
    public bool IsPublished { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    public List<PhotoTag> PhotoTags { get; set; } = [];
    public List<CollectionPhoto> CollectionPhotos { get; set; } = [];
    public List<PhotoVariant> Variants { get; set; } = [];
}

public class Tag
{
    public Guid Id { get; set; }
    public string NameSk { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public List<PhotoTag> PhotoTags { get; set; } = [];
}

public class PhotoTag
{
    public Guid PhotoId { get; set; }
    public Photo Photo { get; set; } = default!;
    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = default!;
}

public class Collection
{
    public Guid Id { get; set; }
    public string NameSk { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? DescriptionSk { get; set; }
    public string? DescriptionEn { get; set; }
    public Guid? CoverPhotoId { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
    public List<CollectionPhoto> CollectionPhotos { get; set; } = [];
}

public class CollectionPhoto
{
    public Guid CollectionId { get; set; }
    public Collection Collection { get; set; } = default!;
    public Guid PhotoId { get; set; }
    public Photo Photo { get; set; } = default!;
    public int SortOrder { get; set; }
}

public class PhotoVariant
{
    public Guid Id { get; set; }
    public Guid PhotoId { get; set; }
    public Photo Photo { get; set; } = default!;
    public int Width { get; set; }
    public int Height { get; set; }
    public string Format { get; set; } = default!;
    public int Quality { get; set; }
    public string BlobUrl { get; set; } = default!;
    public long SizeBytes { get; set; }
}
