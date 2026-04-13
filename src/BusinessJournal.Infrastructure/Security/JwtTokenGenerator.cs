using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BusinessJournal.Application.Contracts.Auth;
using BusinessJournal.Application.Interfaces;
using BusinessJournal.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BusinessJournal.Infrastructure.Security;

public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _options;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public JwtTokenGenerator(IOptions<JwtOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.Issuer))
        {
            throw new ArgumentException("Jwt:Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(_options.Audience))
        {
            throw new ArgumentException("Jwt:Audience is required.");
        }

        if (string.IsNullOrWhiteSpace(_options.SigningKey))
        {
            throw new ArgumentException("Jwt:SigningKey is required.");
        }

        if (_options.ExpirationMinutes <= 0)
        {
            throw new ArgumentException("Jwt:ExpirationMinutes must be greater than zero.");
        }
    }

    public AuthTokenResult Generate(AppUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var now = DateTime.UtcNow;
        var expiresAtUtc = now.AddMinutes(_options.ExpirationMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_options.SigningKey));

        var credentials = new SigningCredentials(
            signingKey,
            SecurityAlgorithms.HmacSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Expires = expiresAtUtc,
            SigningCredentials = credentials
        };

        var token = _tokenHandler.CreateToken(descriptor);
        var accessToken = _tokenHandler.WriteToken(token);

        return new AuthTokenResult(accessToken, expiresAtUtc);
    }
}