using BusinessJournal.Domain.Entities;

namespace BusinessJournal.Application.Interfaces;

public interface IUserRepository
{
    void Add(AppUser user);
    AppUser? FindByEmail(string email);
}