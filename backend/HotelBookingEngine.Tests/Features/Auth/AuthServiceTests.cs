using HotelBookingEngine.Api.Features.Auth;
using HotelBookingEngine.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace HotelBookingEngine.Tests.Features.Auth;

public class AuthServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _dbContext;
    private readonly AuthService _sut;
    private readonly IPasswordHasher<User> _passwordHasher = new PasswordHasher<User>();

    public AuthServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();

        var jwtOptions = Options.Create(new JwtOptions
        {
            Issuer = "HotelBookingEngine.Tests",
            Audience = "HotelBookingEngine.Tests.Client",
            SigningKey = "unit-test-signing-key-at-least-32-characters-long",
            ExpiryMinutes = 60
        });
        var tokenGenerator = new JwtTokenGenerator(jwtOptions);

        _sut = new AuthService(_dbContext, tokenGenerator, _passwordHasher);
    }

    [Fact]
    public async Task LoginAsync_WithCorrectCredentials_ReturnsToken()
    {
        var hash = _passwordHasher.HashPassword(null!, "correct-password");
        _dbContext.Users.Add(new User { Username = "testuser", PasswordHash = hash, Role = Role.Receptionist });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.LoginAsync(
            new LoginRequest { Username = "testuser", Password = "correct-password" }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("testuser", result!.Username);
        Assert.Equal("Receptionist", result.Role);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ReturnsNull()
    {
        var hash = _passwordHasher.HashPassword(null!, "correct-password");
        _dbContext.Users.Add(new User { Username = "testuser", PasswordHash = hash, Role = Role.Receptionist });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.LoginAsync(
            new LoginRequest { Username = "testuser", Password = "wrong-password" }, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_WithUnknownUsername_ReturnsNull()
    {
        var result = await _sut.LoginAsync(
            new LoginRequest { Username = "ghost", Password = "whatever" }, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_UsernameLookupIsCaseInsensitive()
    {
        var hash = _passwordHasher.HashPassword(null!, "correct-password");
        _dbContext.Users.Add(new User { Username = "TestUser", PasswordHash = hash, Role = Role.Admin });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.LoginAsync(
            new LoginRequest { Username = "testuser", Password = "correct-password" }, CancellationToken.None);

        Assert.NotNull(result);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }
}
