using System.Collections.Concurrent;
using BusinessJournal.Application.Interfaces;
using BusinessJournal.Domain.Common;
using BusinessJournal.Domain.Entities;

namespace BusinessJournal.Infrastructure.Repositories;

public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<string, AppUser> _usersByEmail =
        new(StringComparer.OrdinalIgnoreCase);

    public void Add(AppUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (!_usersByEmail.TryAdd(user.Email, user))
        {
            throw new InvalidOperationException("A user with the same email already exists.");
        }
    }

    public AppUser? FindByEmail(string email)
    {
        var normalizedEmail = TextNormalizer.NormalizeOptionalEmail(email);
        if (normalizedEmail is null)
        {
            return null;
        }

        return _usersByEmail.TryGetValue(normalizedEmail, out var user)
            ? user
            : null;
    }
}