using Microsoft.EntityFrameworkCore;

namespace DogPhoto.Infrastructure.Persistence.Blog;

public class BlogDbContext(DbContextOptions<BlogDbContext> options) : DbContext(options)
{
    public const string Schema = "blog";

    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<BlogTag> Tags => Set<BlogTag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Post>(entity =>
        {
            entity.ToTable("posts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.TitleSk).IsRequired().HasMaxLength(512);
            entity.Property(e => e.TitleEn).IsRequired().HasMaxLength(512);
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(512);
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.Property(e => e.ExcerptSk).HasMaxLength(1024);
            entity.Property(e => e.ExcerptEn).HasMaxLength(1024);
            entity.Property(e => e.FeaturedImageUrl).HasMaxLength(1024);
            entity.Property(e => e.Author).HasMaxLength(256);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Draft");
            entity.Property(e => e.MetaTitleSk).HasMaxLength(256);
            entity.Property(e => e.MetaTitleEn).HasMaxLength(256);
            entity.Property(e => e.MetaDescriptionSk).HasMaxLength(512);
            entity.Property(e => e.MetaDescriptionEn).HasMaxLength(512);
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("categories");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.NameSk).IsRequired().HasMaxLength(128);
            entity.Property(e => e.NameEn).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(128);
            entity.HasIndex(e => e.Slug).IsUnique();
        });

        modelBuilder.Entity<PostCategory>(entity =>
        {
            entity.ToTable("post_categories");
            entity.HasKey(e => new { e.PostId, e.CategoryId });
            entity.HasOne(e => e.Post).WithMany(p => p.PostCategories).HasForeignKey(e => e.PostId);
            entity.HasOne(e => e.Category).WithMany(c => c.PostCategories).HasForeignKey(e => e.CategoryId);
        });

        modelBuilder.Entity<BlogTag>(entity =>
        {
            entity.ToTable("tags");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.NameSk).IsRequired().HasMaxLength(128);
            entity.Property(e => e.NameEn).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(128);
            entity.HasIndex(e => e.Slug).IsUnique();
        });

        modelBuilder.Entity<PostTag>(entity =>
        {
            entity.ToTable("post_tags");
            entity.HasKey(e => new { e.PostId, e.TagId });
            entity.HasOne(e => e.Post).WithMany(p => p.PostTags).HasForeignKey(e => e.PostId);
            entity.HasOne(e => e.Tag).WithMany(t => t.PostTags).HasForeignKey(e => e.TagId);
        });
    }
}

public class Post
{
    public Guid Id { get; set; }
    public string TitleSk { get; set; } = default!;
    public string TitleEn { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? ExcerptSk { get; set; }
    public string? ExcerptEn { get; set; }
    public string? ContentMarkdownSk { get; set; }
    public string? ContentMarkdownEn { get; set; }
    public string? ContentHtmlSk { get; set; }
    public string? ContentHtmlEn { get; set; }
    public string? FeaturedImageUrl { get; set; }
    public string? Author { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime? PublishedAt { get; set; }
    public string? MetaTitleSk { get; set; }
    public string? MetaTitleEn { get; set; }
    public string? MetaDescriptionSk { get; set; }
    public string? MetaDescriptionEn { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    public List<PostCategory> PostCategories { get; set; } = [];
    public List<PostTag> PostTags { get; set; } = [];
}

public class Category
{
    public Guid Id { get; set; }
    public string NameSk { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public List<PostCategory> PostCategories { get; set; } = [];
}

public class PostCategory
{
    public Guid PostId { get; set; }
    public Post Post { get; set; } = default!;
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = default!;
}

public class BlogTag
{
    public Guid Id { get; set; }
    public string NameSk { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public List<PostTag> PostTags { get; set; } = [];
}

public class PostTag
{
    public Guid PostId { get; set; }
    public Post Post { get; set; } = default!;
    public Guid TagId { get; set; }
    public BlogTag Tag { get; set; } = default!;
}
