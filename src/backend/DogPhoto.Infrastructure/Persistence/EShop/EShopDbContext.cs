using Microsoft.EntityFrameworkCore;

namespace DogPhoto.Infrastructure.Persistence.EShop;

public class EShopDbContext(DbContextOptions<EShopDbContext> options) : DbContext(options)
{
    public const string Schema = "eshop";

    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<Format> Formats => Set<Format>();
    public DbSet<PaperType> PaperTypes => Set<PaperType>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<ShoppingCart> ShoppingCarts => Set<ShoppingCart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Format>(entity =>
        {
            entity.ToTable("formats");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.NameSk).IsRequired().HasMaxLength(100);
            entity.Property(e => e.NameEn).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0);
        });

        modelBuilder.Entity<PaperType>(entity =>
        {
            entity.ToTable("paper_types");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.NameSk).IsRequired().HasMaxLength(100);
            entity.Property(e => e.NameEn).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.TitleSk).IsRequired().HasMaxLength(256);
            entity.Property(e => e.TitleEn).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.Property(e => e.Currency).IsRequired().HasMaxLength(3).HasDefaultValue("EUR");
            entity.Property(e => e.IsLimitedEdition).HasDefaultValue(false);
            entity.HasQueryFilter(e => e.DeletedAt == null);
            entity.Property<uint>("xmin")
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
            entity.HasMany(e => e.Variants)
                .WithOne(v => v.Product)
                .HasForeignKey(v => v.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductVariant>(entity =>
        {
            entity.ToTable("product_variants");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Price).HasPrecision(10, 2);
            entity.Property(e => e.Sku).HasMaxLength(64);
            entity.HasIndex(e => new { e.ProductId, e.FormatId, e.PaperTypeId }).IsUnique();
            entity.HasOne(e => e.Format).WithMany().HasForeignKey(e => e.FormatId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.PaperType).WithMany().HasForeignKey(e => e.PaperTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50).HasDefaultValue("pending_payment");
            entity.Property(e => e.TotalAmount).HasPrecision(10, 2);
            entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
            entity.Property(e => e.CustomerEmail).HasMaxLength(256);
            entity.Property(e => e.GoPayPaymentId).HasMaxLength(256);
            entity.Property(e => e.ShippingAddressJson).HasColumnType("jsonb");
            entity.Property(e => e.BillingAddressJson).HasColumnType("jsonb");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("order_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.UnitPrice).HasPrecision(10, 2);
            entity.Property(e => e.ProductTitleSk).IsRequired().HasMaxLength(256);
            entity.Property(e => e.ProductTitleEn).IsRequired().HasMaxLength(256);
            entity.Property(e => e.FormatNameSk).HasMaxLength(100);
            entity.Property(e => e.FormatNameEn).HasMaxLength(100);
            entity.Property(e => e.PaperTypeNameSk).HasMaxLength(100);
            entity.Property(e => e.PaperTypeNameEn).HasMaxLength(100);
            entity.HasOne(e => e.Order).WithMany(o => o.Items).HasForeignKey(e => e.OrderId);
        });

        modelBuilder.Entity<ShoppingCart>(entity =>
        {
            entity.ToTable("shopping_carts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.ToTable("cart_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.HasOne(e => e.Cart).WithMany(c => c.Items).HasForeignKey(e => e.CartId);
            entity.HasOne(e => e.Variant).WithMany().HasForeignKey(e => e.VariantId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

public class Format
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string NameSk { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public int DisplayOrder { get; set; }
}

public class PaperType
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string NameSk { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public int DisplayOrder { get; set; }
}

public class Product
{
    public Guid Id { get; set; }
    public Guid? PhotoId { get; set; }
    public string TitleSk { get; set; } = default!;
    public string TitleEn { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? DescriptionSk { get; set; }
    public string? DescriptionEn { get; set; }
    public string Currency { get; set; } = "EUR";
    public bool IsLimitedEdition { get; set; }
    public int? EditionSize { get; set; }
    public int EditionSold { get; set; }
    public bool IsAvailable { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
    public List<ProductVariant> Variants { get; set; } = [];
}

public class ProductVariant
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public Guid FormatId { get; set; }
    public Format Format { get; set; } = default!;
    public Guid PaperTypeId { get; set; }
    public PaperType PaperType { get; set; } = default!;
    public decimal Price { get; set; }
    public string? Sku { get; set; }
    public bool IsAvailable { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Order
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Status { get; set; } = "pending_payment";
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "EUR";
    public string? CustomerEmail { get; set; }
    public string? GoPayPaymentId { get; set; }
    public string? ShippingAddressJson { get; set; }
    public string? BillingAddressJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<OrderItem> Items { get; set; } = [];
}

public class OrderItem
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = default!;
    public Guid ProductId { get; set; }
    public Guid VariantId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public int? EditionNumber { get; set; }

    // Snapshots so order history survives variant/product edits or deletes.
    public string ProductTitleSk { get; set; } = default!;
    public string ProductTitleEn { get; set; } = default!;
    public string? FormatNameSk { get; set; }
    public string? FormatNameEn { get; set; }
    public string? PaperTypeNameSk { get; set; }
    public string? PaperTypeNameEn { get; set; }
}

public class ShoppingCart
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<CartItem> Items { get; set; } = [];
}

public class CartItem
{
    public Guid Id { get; set; }
    public Guid CartId { get; set; }
    public ShoppingCart Cart { get; set; } = default!;
    public Guid VariantId { get; set; }
    public ProductVariant Variant { get; set; } = default!;
    public int Quantity { get; set; }
}
