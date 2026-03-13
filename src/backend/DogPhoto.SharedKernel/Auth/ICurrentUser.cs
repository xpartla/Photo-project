namespace DogPhoto.SharedKernel.Auth;

public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Email { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }
}

public static class Roles
{
    public const string Customer = "Customer";
    public const string Admin = "Admin";
}
