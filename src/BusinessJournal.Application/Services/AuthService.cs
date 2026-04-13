using BusinessJournal.Application.Common.Exceptions;
using BusinessJournal.Application.Contracts.Auth;
using BusinessJournal.Application.Interfaces;
using BusinessJournal.Domain.Common;
using BusinessJournal.Domain.Entities;

namespace BusinessJournal.Application.Services;

public sealed class AuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        ArgumentNullException.ThrowIfNull(userRepository);
        ArgumentNullException.ThrowIfNull(passwordHasher);
        ArgumentNullException.ThrowIfNull(jwtTokenGenerator);

        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public AuthTokenResult Login(string email, string password)
    {
        var normalizedEmail = TextNormalizer.NormalizeRequiredEmail(
            email,
            nameof(email),
            "Email is required.");

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password is required.", nameof(password));
        }

        var user = _userRepository.FindByEmail(normalizedEmail);
        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedException("Invalid email or password.");
        }

        var isValidPassword = _passwordHasher.Verify(user.PasswordHash, password);
        if (!isValidPassword)
        {
            throw new UnauthorizedException("Invalid email or password.");
        }

        return _jwtTokenGenerator.Generate(user);
    }
}