namespace DogPhoto.Infrastructure.Auth;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = default!;
    public string Issuer { get; set; } = "DogPhoto";
    public string Audience { get; set; } = "DogPhoto";
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
