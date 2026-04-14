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

        // Seed booking session types
        var bookingDb = sp.GetRequiredService<BookingDbContext>();
        if (!await bookingDb.SessionTypes.AnyAsync())
        {
            bookingDb.SessionTypes.AddRange(
                new SessionType
                {
                    NameSk = "Psí portrét",
                    NameEn = "Dog Portrait",
                    Slug = "dog-portrait",
                    DescriptionSk = "Profesionálne portréty vášho psa v štúdiu alebo exteriéri.",
                    DescriptionEn = "Professional portraits of your dog in studio or outdoor setting.",
                    DurationMinutes = 60,
                    BasePrice = 120,
                    Category = "portrait",
                    MaxDogs = 2,
                    IncludesJson = """["10 edited photos", "online gallery", "print rights"]"""
                },
                new SessionType
                {
                    NameSk = "Akčné fotenie",
                    NameEn = "Action Session",
                    Slug = "action-session",
                    DescriptionSk = "Dynamické zábery vášho psa v pohybe.",
                    DescriptionEn = "Dynamic shots of your dog in motion.",
                    DurationMinutes = 90,
                    BasePrice = 150,
                    Category = "action",
                    MaxDogs = 3,
                    IncludesJson = """["15 edited photos", "online gallery", "print rights"]"""
                },
                // Studio session — temporarily disabled
                // new SessionType
                // {
                //     NameSk = "Štúdiové fotenie",
                //     NameEn = "Studio Session",
                //     Slug = "studio-session",
                //     DescriptionSk = "Fotenie v profesionálnom štúdiu s rôznymi pozadiami.",
                //     DescriptionEn = "Photography in professional studio with various backdrops.",
                //     DurationMinutes = 45,
                //     BasePrice = 100,
                //     Category = "studio",
                //     MaxDogs = 1,
                //     IncludesJson = """["8 edited photos", "online gallery", "print rights"]"""
                // },
                new SessionType
                {
                    NameSk = "Exteriérové fotenie",
                    NameEn = "Outdoor Session",
                    Slug = "outdoor-session",
                    DescriptionSk = "Fotenie v prírode alebo mestskom prostredí Bratislavy.",
                    DescriptionEn = "Photography in nature or urban Bratislava settings.",
                    DurationMinutes = 90,
                    BasePrice = 140,
                    Category = "outdoor",
                    MaxDogs = 3,
                    IncludesJson = """["12 edited photos", "online gallery", "print rights"]"""
                }
            );
            await bookingDb.SaveChangesAsync();
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
    }
}
