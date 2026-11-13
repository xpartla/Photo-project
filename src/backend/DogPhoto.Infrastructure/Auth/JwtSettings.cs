namespace DogPhoto.Infrastructure.Auth;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    // HMAC-SHA256 needs a key of at least 256 bits.
    public const int MinSecretBytes = 32;

    public string Secret { get; set; } = default!;
    public string Issuer { get; set; } = "DogPhoto";
    public string Audience { get; set; } = "DogPhoto";
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
