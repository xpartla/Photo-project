using System.Text;
using DogPhoto.Infrastructure.Auth;
using DogPhoto.Infrastructure.BlobStorage;
using DogPhoto.Infrastructure.Email;
using DogPhoto.Infrastructure.ImagePipeline;
using DogPhoto.Infrastructure.Payments;
using DogPhoto.Infrastructure.Persistence.Blog;
using DogPhoto.Infrastructure.Persistence.Booking;
using DogPhoto.Infrastructure.Persistence.EShop;
using DogPhoto.Infrastructure.Persistence.Identity;
using DogPhoto.Infrastructure.Persistence.Portfolio;
using DogPhoto.SharedKernel.Auth;
using DogPhoto.SharedKernel.Email;
using DogPhoto.SharedKernel.Events;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace DogPhoto.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' not found.");

        // DbContexts — one per module schema
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(connectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema)));
        services.AddDbContext<PortfolioDbContext>(options =>
            options.UseNpgsql(connectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", PortfolioDbContext.Schema)));
        services.AddDbContext<EShopDbContext>(options =>
            options.UseNpgsql(connectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", EShopDbContext.Schema)));
        services.AddDbContext<BookingDbContext>(options =>
            options.UseNpgsql(connectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", BookingDbContext.Schema)));
        services.AddDbContext<BlogDbContext>(options =>
            options.UseNpgsql(connectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", BlogDbContext.Schema)));

        // Auth
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? new JwtSettings { Secret = configuration["Jwt:Secret"] ?? "dev-secret-key-minimum-32-characters-long!!" };

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        // Ensure JwtSettings is always available even if section is missing
        if (string.IsNullOrEmpty(jwtSettings.Secret))
            jwtSettings.Secret = "dev-secret-key-minimum-32-characters-long!!";

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,
                ValidateLifetime = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                ClockSkew = TimeSpan.Zero
            };
        });

        services.AddAuthorization();

        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddHttpContextAccessor();

        // Blob storage
        services.AddSingleton<IBlobStorageService, BlobStorageService>();

        // Email
        services.Configure<SmtpSettings>(configuration.GetSection(SmtpSettings.SectionName));
        services.AddScoped<IEmailService, SmtpEmailService>();

        // Payment gateway
        services.AddSingleton<MockPaymentGateway>();
        services.AddSingleton<IPaymentGateway>(sp => sp.GetRequiredService<MockPaymentGateway>());

        // Image pipeline
        services.AddScoped<IImageProcessor, ImageProcessor>();
        var processingQueue = new ImageProcessingQueue();
        services.AddSingleton(processingQueue);
        services.AddSingleton<IImageProcessingQueue>(processingQueue);
        services.AddHostedService<ImageProcessingWorker>();

        return services;
    }

    public static async Task ApplyMigrationsAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var env = sp.GetRequiredService<IHostEnvironment>();

        if (env.IsDevelopment())
        {
            // In development, create schemas and tables from model directly
            // This avoids needing migration files during early development
            var contexts = new DbContext[]
            {
                sp.GetRequiredService<IdentityDbContext>(),
                sp.GetRequiredService<PortfolioDbContext>(),
                sp.GetRequiredService<EShopDbContext>(),
                sp.GetRequiredService<BookingDbContext>(),
                sp.GetRequiredService<BlogDbContext>(),
            };

            // Ensure database exists using first context
            await contexts[0].Database.EnsureCreatedAsync();

            // Dev-only schema patches for identity: additive changes (new column,
            // new table) that EnsureCreated skips on an already-existing schema.
            // Kept idempotent via IF NOT EXISTS so re-runs are safe.
            var identityDb = sp.GetRequiredService<IdentityDbContext>();
            await identityDb.Database.ExecuteSqlRawAsync(
                "ALTER TABLE identity.users ADD COLUMN IF NOT EXISTS \"Phone\" varchar(50)");
            await identityDb.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS identity.addresses (
                    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                    "UserId" uuid NOT NULL REFERENCES identity.users("Id") ON DELETE CASCADE,
                    "Label" varchar(64),
                    "Name" varchar(256) NOT NULL,
                    "Street" varchar(256) NOT NULL,
                    "City" varchar(128) NOT NULL,
                    "PostalCode" varchar(32) NOT NULL,
                    "Country" varchar(2) NOT NULL DEFAULT 'SK',
                    "IsDefaultShipping" boolean NOT NULL DEFAULT false,
                    "IsDefaultBilling" boolean NOT NULL DEFAULT false,
                    "CreatedAt" timestamptz NOT NULL DEFAULT now(),
                    "UpdatedAt" timestamptz NOT NULL DEFAULT now()
                )
                """);
            await identityDb.Database.ExecuteSqlRawAsync(
                "CREATE INDEX IF NOT EXISTS \"IX_addresses_UserId\" ON identity.addresses(\"UserId\")");

            // Dev-only: rebuild the eshop schema when its model has changed in a
            // backwards-incompatible way. Detection is by presence of a table that
            // only exists in the latest model. Product data is re-seeded by seed-shop.sh.
            var eshopDb = sp.GetRequiredService<EShopDbContext>();
            var variantsTableExists = await eshopDb.Database
                .SqlQueryRaw<bool>("SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'eshop' AND table_name = 'product_variants') AS \"Value\"")
                .FirstAsync();
            if (!variantsTableExists)
            {
                await eshopDb.Database.ExecuteSqlRawAsync("DROP SCHEMA IF EXISTS eshop CASCADE");
            }

            // For remaining contexts, create schemas and tables via SQL generation
            foreach (var context in contexts.Skip(1))
            {
                var internalSp = ((Microsoft.EntityFrameworkCore.Infrastructure.IInfrastructure<IServiceProvider>)context).Instance;
                var creator = (Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator)
                    internalSp.GetRequiredService<Microsoft.EntityFrameworkCore.Storage.IDatabaseCreator>();
                try { await creator.CreateTablesAsync(); } catch { /* Tables already exist */ }
            }
        }
        else
        {
            // In production, use proper migrations
            await sp.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
            await sp.GetRequiredService<PortfolioDbContext>().Database.MigrateAsync();
            await sp.GetRequiredService<EShopDbContext>().Database.MigrateAsync();
            await sp.GetRequiredService<BookingDbContext>().Database.MigrateAsync();
            await sp.GetRequiredService<BlogDbContext>().Database.MigrateAsync();
        }
    }

    public static async Task SeedDataAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        // Seed / reconcile booking session types — the dog-only launch packages.
        // This runs every startup (not just on an empty table) so edits to the
        // canonical package content take effect without a manual DB reset.
        // Rows are matched by slug; any other active session type (e.g. retired
        // seed data) is deactivated so it drops out of the public listing while
        // keeping existing bookings' foreign-key references intact.
        // `includes` is stored bilingually as {"sk":[...],"en":[...]}.
        var bookingDb = sp.GetRequiredService<BookingDbContext>();
        var desiredSessionTypes = new[]
        {
            new SessionType
            {
                NameSk = "Legacy",
                NameEn = "Legacy",
                Slug = "legacy",
                DescriptionSk = "24 profesionálnych fotografií na kvalitný 35 mm film. Po vyvolaní a digitalizácii dostanete celú rolku analógu, jednu vybranú fotografiu vytlačenú v galerijnej kvalite a všetky zábery v online galérii.",
                DescriptionEn = "24 professional photographs on quality 35 mm film. After developing and digitising you receive the whole roll of analogue shots, one selected photo printed in gallery quality, and every image in an online gallery.",
                DurationMinutes = 90,
                BasePrice = 180,
                Category = "film",
                MaxDogs = 1,
                IncludesJson = """{"sk":["24 analógových fotografií","online galéria","vybrané výtlačky"],"en":["24 analog photos","online gallery","selected prints"]}"""
            },
            new SessionType
            {
                NameSk = "Digital",
                NameEn = "Digital",
                Slug = "digital",
                DescriptionSk = "15 kvalitných, profesionálne upravených fotografií fotených v štúdiu alebo v exteriéri.",
                DescriptionEn = "15 high-quality, professionally edited photographs taken in the studio or outdoors.",
                DurationMinutes = 60,
                BasePrice = 130,
                Category = "digital",
                MaxDogs = 1,
                IncludesJson = """{"sk":["15 profesionálne upravených fotografií","online galéria","vybrané výtlačky"],"en":["15 professionally edited photos","online gallery","selected prints"]}"""
            },
            new SessionType
            {
                NameSk = "Action",
                NameEn = "Action",
                Slug = "action",
                DescriptionSk = "12 profesionálne upravených fotografií vášho psa v pohybe, fotených v prírode — či už beží za loptičkou alebo sa rúti za vami.",
                DescriptionEn = "12 professionally edited photographs of your dog in motion, shot outdoors — whether chasing a ball or racing toward you.",
                DurationMinutes = 90,
                BasePrice = 150,
                Category = "action",
                MaxDogs = 2,
                IncludesJson = """{"sk":["12 profesionálne upravených fotografií","online galéria","vybrané výtlačky"],"en":["12 professionally edited photos","online gallery","selected prints"]}"""
            }
        };

        var existingSessionTypes = await bookingDb.SessionTypes.ToListAsync();
        var sessionTypesBySlug = existingSessionTypes.ToDictionary(s => s.Slug);
        foreach (var desired in desiredSessionTypes)
        {
            if (sessionTypesBySlug.TryGetValue(desired.Slug, out var row))
            {
                row.NameSk = desired.NameSk;
                row.NameEn = desired.NameEn;
                row.DescriptionSk = desired.DescriptionSk;
                row.DescriptionEn = desired.DescriptionEn;
                row.DurationMinutes = desired.DurationMinutes;
                row.BasePrice = desired.BasePrice;
                row.Category = desired.Category;
                row.MaxDogs = desired.MaxDogs;
                row.IncludesJson = desired.IncludesJson;
                row.IsActive = true;
                row.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                bookingDb.SessionTypes.Add(desired);
            }
        }

        var desiredSlugs = desiredSessionTypes.Select(d => d.Slug).ToHashSet();
        foreach (var stale in existingSessionTypes.Where(s => s.IsActive && !desiredSlugs.Contains(s.Slug)))
        {
            stale.IsActive = false;
            stale.UpdatedAt = DateTime.UtcNow;
        }

        await bookingDb.SaveChangesAsync();

        // Seed eshop lookup tables (formats & paper types)
        var eshopDb = sp.GetRequiredService<EShopDbContext>();
        if (!await eshopDb.Formats.AnyAsync())
        {
            eshopDb.Formats.AddRange(
                new Persistence.EShop.Format { Code = "a4", NameSk = "A4 (21×30 cm)", NameEn = "A4 (21×30 cm)", DisplayOrder = 1 },
                new Persistence.EShop.Format { Code = "a3", NameSk = "A3 (30×42 cm)", NameEn = "A3 (30×42 cm)", DisplayOrder = 2 },
                new Persistence.EShop.Format { Code = "30x40", NameSk = "30×40 cm", NameEn = "30×40 cm", DisplayOrder = 3 },
                new Persistence.EShop.Format { Code = "40x60", NameSk = "40×60 cm", NameEn = "40×60 cm", DisplayOrder = 4 },
                new Persistence.EShop.Format { Code = "50x70", NameSk = "50×70 cm", NameEn = "50×70 cm", DisplayOrder = 5 }
            );
            await eshopDb.SaveChangesAsync();
        }
        if (!await eshopDb.PaperTypes.AnyAsync())
        {
            eshopDb.PaperTypes.AddRange(
                new Persistence.EShop.PaperType { Code = "fine-art-310", NameSk = "Fine Art 310g (Hahnemühle)", NameEn = "Fine Art 310g (Hahnemühle)", DisplayOrder = 1 },
                new Persistence.EShop.PaperType { Code = "baryta", NameSk = "Baryta Photographique (Canson)", NameEn = "Baryta Photographique (Canson)", DisplayOrder = 2 },
                new Persistence.EShop.PaperType { Code = "matte-200", NameSk = "Matný 200g", NameEn = "Matte 200g", DisplayOrder = 3 },
                new Persistence.EShop.PaperType { Code = "glossy-premium", NameSk = "Premium lesklý", NameEn = "Premium Glossy", DisplayOrder = 4 }
            );
            await eshopDb.SaveChangesAsync();
        }

        // Seed blog categories
        var blogDb = sp.GetRequiredService<BlogDbContext>();
        if (!await blogDb.Categories.AnyAsync())
        {
            blogDb.Categories.AddRange(
                new Category { NameSk = "Tipy na fotenie", NameEn = "Photography Tips", Slug = "photography-tips" },
                new Category { NameSk = "Za scénou", NameEn = "Behind the Scenes", Slug = "behind-the-scenes" },
                new Category { NameSk = "Lokality", NameEn = "Locations", Slug = "locations" },
                new Category { NameSk = "Vybavenie", NameEn = "Equipment", Slug = "equipment" }
            );
            await blogDb.SaveChangesAsync();
        }

        // Seed admin user
        var identityDb = sp.GetRequiredService<IdentityDbContext>();
        if (!await identityDb.Users.AnyAsync(u => u.Role == "Admin"))
        {
            identityDb.Users.Add(new User
            {
                Email = "admin@dogphoto.sk",
                DisplayName = "Admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Role = "Admin"
            });
            await identityDb.SaveChangesAsync();
        }

        // Seed regular customer (for local testing — exercises the Customer role)
        if (!await identityDb.Users.AnyAsync(u => u.Email == "customer@dogphoto.sk"))
        {
            identityDb.Users.Add(new User
            {
                Email = "customer@dogphoto.sk",
                DisplayName = "Customer",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("customer123"),
                Role = "Customer"
            });
            await identityDb.SaveChangesAsync();
        }
    }
}
