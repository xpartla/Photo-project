namespace DogPhoto.Infrastructure.Email;

public class NotificationSettings
{
    public const string SectionName = "Notifications";

    /// <summary>Receives the "new booking" and "new order" notifications.</summary>
    public string PhotographerEmail { get; set; } = "admin@partlphoto.sk";
}
