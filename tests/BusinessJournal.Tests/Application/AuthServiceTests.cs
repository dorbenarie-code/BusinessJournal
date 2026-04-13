using BusinessJournal.Application.Common.Exceptions;
using BusinessJournal.Application.Contracts.Auth;
using BusinessJournal.Application.Interfaces;
using BusinessJournal.Application.Services;
using BusinessJournal.Domain.Entities;
using Xunit;

namespace BusinessJournal.Tests.Application;

public sealed class AuthServiceTests
{
    [Fact]
    public void Login_WithExistingActiveUserAndValidPassword_ShouldReturnToken()
    {
        var user = AppUser.Create(
            "admin@businessjournal.com",
            "HASH::secret123",
            "Admin");

        var userRepository = new FakeUserRepository(user);
        var passwordHasher = new FakePasswordHasher();
        var expectedToken = new AuthTokenResult(
            "fake-jwt-token",
            new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        var tokenGenerator = new FakeJwtTokenGenerator(expectedToken);

        var service = new AuthService(userRepository, passwordHasher, tokenGenerator);

        var result = service.Login("ADMIN@BusinessJournal.com", "secret123");

        Assert.NotNull(result);
        Assert.Equal(expectedToken.AccessToken, result.AccessToken);
        Assert.Equal(expectedToken.ExpiresAtUtc, result.ExpiresAtUtc);
        Assert.Equal(user.Id, tokenGenerator.LastGeneratedForUserId);
    }

    [Fact]
    public void Login_WhenUserDoesNotExist_ShouldThrowUnauthorizedException()
    {
        var userRepository = new FakeUserRepository();
        var passwordHasher = new FakePasswordHasher();
        var tokenGenerator = new FakeJwtTokenGenerator();

        var service = new AuthService(userRepository, passwordHasher, tokenGenerator);

        Assert.Throws<UnauthorizedException>(() =>
            service.Login("missing@businessjournal.com", "secret123"));
    }

    [Fact]
    public void Login_WhenUserIsInactive_ShouldThrowUnauthorizedException()
    {
        var user = AppUser.Create(
            "admin@businessjournal.com",
            "HASH::secret123",
            "Admin");

        user.Deactivate();

        var userRepository = new FakeUserRepository(user);
        var passwordHasher = new FakePasswordHasher();
        var tokenGenerator = new FakeJwtTokenGenerator();

        var service = new AuthService(userRepository, passwordHasher, tokenGenerator);

        Assert.Throws<UnauthorizedException>(() =>
            service.Login("admin@businessjournal.com", "secret123"));
    }

    [Fact]
    public void Login_WhenPasswordIsInvalid_ShouldThrowUnauthorizedException()
    {
        var user = AppUser.Create(
            "admin@businessjournal.com",
            "HASH::secret123",
            "Admin");

        var userRepository = new FakeUserRepository(user);
        var passwordHasher = new FakePasswordHasher();
        var tokenGenerator = new FakeJwtTokenGenerator();

        var service = new AuthService(userRepository, passwordHasher, tokenGenerator);

        Assert.Throws<UnauthorizedException>(() =>
            service.Login("admin@businessjournal.com", "wrong-password"));
    }

    [Fact]
    public void Login_WithEmptyEmail_ShouldThrowArgumentException()
    {
        var userRepository = new FakeUserRepository();
        var passwordHasher = new FakePasswordHasher();
        var tokenGenerator = new FakeJwtTokenGenerator();

        var service = new AuthService(userRepository, passwordHasher, tokenGenerator);

        Assert.Throws<ArgumentException>(() =>
            service.Login("   ", "secret123"));
    }

    [Fact]
    public void Login_WithEmptyPassword_ShouldThrowArgumentException()
    {
        var userRepository = new FakeUserRepository();
        var passwordHasher = new FakePasswordHasher();
        var tokenGenerator = new FakeJwtTokenGenerator();

        var service = new AuthService(userRepository, passwordHasher, tokenGenerator);

        Assert.Throws<ArgumentException>(() =>
            service.Login("admin@businessjournal.com", "   "));
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly Dictionary<string, AppUser> _usersByEmail;

        public FakeUserRepository(params AppUser[] users)
        {
            _usersByEmail = users.ToDictionary(
                user => user.Email,
                user => user,
                StringComparer.OrdinalIgnoreCase);
        }

        public void Add(AppUser user)
        {
            ArgumentNullException.ThrowIfNull(user);

            _usersByEmail[user.Email] = user;
        }

        public AppUser? FindByEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            return _usersByEmail.TryGetValue(email.Trim().ToLowerInvariant(), out var user)
                ? user
                : null;
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password)
        {
            return $"HASH::{password}";
        }

        public bool Verify(string passwordHash, string providedPassword)
        {
            return passwordHash == $"HASH::{providedPassword}";
        }
    }

    private sealed class FakeJwtTokenGenerator : IJwtTokenGenerator
    {
        private readonly AuthTokenResult _result;

        public FakeJwtTokenGenerator()
            : this(new AuthTokenResult(
                "default-fake-token",
                new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc)))
        {
        }

        public FakeJwtTokenGenerator(AuthTokenResult result)
        {
            _result = result;
        }

        public Guid? LastGeneratedForUserId { get; private set; }

        public AuthTokenResult Generate(AppUser user)
        {
            ArgumentNullException.ThrowIfNull(user);

            LastGeneratedForUserId = user.Id;
            return _result;
        }
    }
}