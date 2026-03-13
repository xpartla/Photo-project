using Microsoft.EntityFrameworkCore;

namespace DogPhoto.Infrastructure.Persistence.EShop;

public class EShopDbContext(DbContextOptions<EShopDbContext> options) : DbContext(options)
{
    public const string Schema = "eshop";

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<ShoppingCart> ShoppingCarts => Set<ShoppingCart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.TitleSk).IsRequired().HasMaxLength(256);
            entity.Property(e => e.TitleEn).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.Property(e => e.Format).HasMaxLength(50);
            entity.Property(e => e.PaperType).HasMaxLength(100);
            entity.Property(e => e.Price).HasPrecision(10, 2);
            entity.Property(e => e.Currency).IsRequired().HasMaxLength(3).HasDefaultValue("EUR");
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.TotalAmount).HasPrecision(10, 2);
            entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
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
        });
    }
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
    public string? Format { get; set; }
    public string? PaperType { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "EUR";
    public int? EditionSize { get; set; }
    public int EditionSold { get; set; }
    public bool IsAvailable { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
}

public class Order
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Status { get; set; } = "Pending";
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "EUR";
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
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public int? EditionNumber { get; set; }
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
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}
