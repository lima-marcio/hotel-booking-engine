using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HotelBookingEngine.Api.Features.Auth;
using Microsoft.Extensions.Options;
using Xunit;

namespace HotelBookingEngine.Tests.Features.Auth;

public class JwtTokenGeneratorTests
{
    private static JwtTokenGenerator CreateSut()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "HotelBookingEngine.Tests",
            Audience = "HotelBookingEngine.Tests.Client",
            SigningKey = "unit-test-signing-key-at-least-32-characters-long",
            ExpiryMinutes = 60
        });

        return new JwtTokenGenerator(options);
    }

    [Fact]
    public void GenerateToken_IncludesExpectedClaims()
    {
        var sut = CreateSut();
        var user = new User { Id = 42, Username = "receptionist1", PasswordHash = "irrelevant", Role = Role.Receptionist };

        var (token, _) = sut.GenerateToken(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("42", jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("receptionist1", jwt.Claims.Single(c => c.Type == ClaimTypes.Name).Value);
        Assert.Equal("Receptionist", jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public void GenerateToken_ExpiryMatchesConfiguredMinutes()
    {
        var sut = CreateSut();
        var user = new User { Id = 1, Username = "admin", PasswordHash = "irrelevant", Role = Role.Admin };
        var before = DateTime.UtcNow;

        var (_, expiresAtUtc) = sut.GenerateToken(user);

        var after = DateTime.UtcNow;
        Assert.InRange(expiresAtUtc, before.AddMinutes(60), after.AddMinutes(60));
    }
}
