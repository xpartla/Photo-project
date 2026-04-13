namespace DogPhoto.Infrastructure.Email;

public class SmtpSettings
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string FromEmail { get; set; } = "info@partlphoto.sk";
    public string FromName { get; set; } = "PartlPhoto";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseSsl { get; set; }
}
