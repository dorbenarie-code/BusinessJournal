using BusinessJournal.Application.Contracts.Auth;
using BusinessJournal.Domain.Entities;

namespace BusinessJournal.Application.Interfaces;

public interface IJwtTokenGenerator
{
    AuthTokenResult Generate(AppUser user);
}