using BusinessJournal.Domain.Common;

namespace BusinessJournal.Domain.Entities;

public sealed class AppUser
{
    public Guid Id { get; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public string Role { get; private set; }
    public bool IsActive { get; private set; }

    private AppUser(
        Guid id,
        string email,
        string passwordHash,
        string role,
        bool isActive)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(id));
        }

        Id = id;
        Email = TextNormalizer.NormalizeRequiredEmail(email);
        PasswordHash = TextNormalizer.NormalizeRequired(passwordHash, nameof(passwordHash), "Password hash is required.");
        Role = TextNormalizer.NormalizeRequired(role, nameof(role), "Role is required.");
        IsActive = isActive;
    }

    public static AppUser Create(
        string email,
        string passwordHash,
        string role = "Admin")
    {
        return new AppUser(
            Guid.NewGuid(),
            email,
            passwordHash,
            role,
            true);
    }

    public static AppUser Restore(
        Guid id,
        string email,
        string passwordHash,
        string role,
        bool isActive)
    {
        return new AppUser(id, email, passwordHash, role, isActive);
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}