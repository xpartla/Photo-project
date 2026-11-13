namespace DogPhoto.Infrastructure.RateLimiting;

public class RateLimitSettings
{
    public const string SectionName = "RateLimiting";

    public RateLimitRule Auth { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };
    public RateLimitRule Booking { get; set; } = new() { PermitLimit = 5, WindowSeconds = 60 };
}

public class RateLimitRule
{
    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; }
}
