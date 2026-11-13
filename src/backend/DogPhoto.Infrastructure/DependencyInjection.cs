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
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
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
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
        if (Encoding.UTF8.GetByteCount(jwtSettings.Secret ?? string.Empty) < JwtSettings.MinSecretBytes)
            throw new InvalidOperationException(
                $"Jwt:Secret is missing or shorter than {JwtSettings.MinSecretBytes} bytes. " +
                "Set it in configuration (environment variable Jwt__Secret).");

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

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
        services.Configure<NotificationSettings>(configuration.GetSection(NotificationSettings.SectionName));
        services.AddScoped<IEmailService, SmtpEmailService>();

        // Payment gateway
        if (environment.IsDevelopment())
        {
            services.AddSingleton<MockPaymentGateway>();
            services.AddSingleton<IPaymentGateway>(sp => sp.GetRequiredService<MockPaymentGateway>());
        }
        else
        {
            services.AddSingleton<IPaymentGateway, UnavailablePaymentGateway>();
        }

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

        var contexts = new (DbContext Context, string Schema)[]
        {
            (sp.GetRequiredService<IdentityDbContext>(), IdentityDbContext.Schema),
            (sp.GetRequiredService<PortfolioDbContext>(), PortfolioDbContext.Schema),
            (sp.GetRequiredService<EShopDbContext>(), EShopDbContext.Schema),
            (sp.GetRequiredService<BookingDbContext>(), BookingDbContext.Schema),
            (sp.GetRequiredService<BlogDbContext>(), BlogDbContext.Schema),
        };

        foreach (var (context, schema) in contexts)
        {
            await ThrowIfCreatedWithoutMigrationsAsync(context, schema);
            await context.Database.MigrateAsync();
        }
    }

    // Databases from before migrations existed were built with EnsureCreated:
    // the tables are there but the history table is not, so Migrate() would
    // fail on the first CREATE TABLE with a far less helpful message.
    private static async Task ThrowIfCreatedWithoutMigrationsAsync(DbContext context, string schema)
    {
        if (!await context.Database.CanConnectAsync()) return;

        var legacy = await context.Database
            .SqlQuery<bool>($"""
                SELECT EXISTS (SELECT 1 FROM information_schema.tables
                               WHERE table_schema = {schema} AND table_name <> '__EFMigrationsHistory')
                   AND NOT EXISTS (SELECT 1 FROM information_schema.tables
                                   WHERE table_schema = {schema} AND table_name = '__EFMigrationsHistory') AS "Value"
                """)
            .SingleAsync();

        if (legacy)
            throw new InvalidOperationException(
                $"Schema '{schema}' was created without EF migrations and cannot be upgraded in place. " +
                "For a local database, reset it with `docker compose down -v` and start the stack again.");
    }

    public static async Task SeedDataAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        // Starter booking packages. Only packages whose slug is missing are
        // inserted: once a row exists the admin UI owns it, so edits and
        // deactivations survive restarts.
        // `includes` is stored bilingually as {"sk":[...],"en":[...]}.
        var bookingDb = sp.GetRequiredService<BookingDbContext>();
        var starterPackages = new[]
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

        var existingSlugs = await bookingDb.SessionTypes.Select(s => s.Slug).ToListAsync();
        bookingDb.SessionTypes.AddRange(starterPackages.Where(d => !existingSlugs.Contains(d.Slug)));
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
