using SharedKernel.BaseEntities;
using AuthService.Domain.Enums;

namespace AuthService.Domain.Entities;

public class User : BaseEntity
{
    public string Username { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    public string Phone { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public UserStatus Status { get; private set; }

    private User() { }

    public static User Create(string username, string passwordHash, string fullName, string phone, UserRole role = UserRole.Customer)
    {
        return new User
        {
            Username = username,
            PasswordHash = passwordHash,
            FullName = fullName,
            Phone = phone,
            Role = role,
            Status = UserStatus.Active
        };
    }

    public void UpdateInfo(string fullName, string phone)
    {
        FullName = fullName;
        Phone = phone;
        SetUpdated();
    }

    public void Ban() { Status = UserStatus.Banned; SetUpdated(); }
    public void Activate() { Status = UserStatus.Active; SetUpdated(); }
}
