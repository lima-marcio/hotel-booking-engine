# Phase 2 - Authentication Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Login, JWT issuance/validation, and role-based authorization (Admin, Receptionist) to the Hotel Booking Engine, backend and frontend, demonstrable end-to-end in a browser.

**Architecture:** First real entity (`User`) lands in `AppDbContext` via a Fluent API configuration with a seeded Admin row. A `Features/Auth/` vertical slice (same shape as Phase 1's `Features/Health/`) issues and validates JWTs: `AuthService` verifies credentials against the hashed password and returns a token via `IJwtTokenGenerator`; `AuthController` exposes `POST /api/auth/login` and an `[Authorize]`-protected `GET /api/auth/me` that proves role claims round-trip correctly. The frontend adds a login page, a small token store + React Context for auth state, an Axios interceptor that attaches the bearer token, and extends the existing Home page to show the logged-in identity.

**Tech Stack:** `Microsoft.Extensions.Identity.Core` (`PasswordHasher<T>`, no EF/Identity stores), `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.AspNetCore.Mvc.Testing` (test project only) — React Hook Form + Zod (already installed) for the login form, `@hookform/resolvers` (new) to bridge them.

**Design spec:** `docs/superpowers/specs/2026-07-24-phase2-authentication-design.md` — read for full rationale; this plan implements it as-is.

## Global Constraints

- Roles: `Admin` and `Receptionist` only, modeled as a C# enum stored as a string — no separate roles table.
- No self-registration. A single Admin user is seeded via EF Core migration `HasData`: `Username = "admin"`, password `Admin123!`, pre-computed hash `AQAAAAIAAYagAAAAEFcUQUh++8uhwIPl5ZLbH9pqYcVgsjXD2wd02MD3xXjMRNh2hWOOXD3s/J8LnDygRA==` (verified against `PasswordHasher<T>.VerifyHashedPassword` before being written into this plan — use it verbatim, do not regenerate it).
- Login credential is `Username`, not email. Username lookup must be case-insensitive and behave identically on SQLite (dev) and SQL Server (prod) — normalize with `.ToLowerInvariant()` / `.ToLower()` on both sides of the comparison inside the EF query (translates to `LOWER()` on both providers), not a provider-specific collation setting.
- `GET /api/auth/me` is the vehicle for proving role-based authorization works — no other business endpoint exists yet to protect (Hotels/Rooms arrive in Phase 3).
- No `ProtectedRoute` frontend route guard in this phase (deferred to Phase 3, when the first admin-only page exists).
- No refresh tokens. Access token expires in 60 minutes; re-login is the only path back.
- No frontend automated tests (Vitest/RTL are not part of the approved frontend stack). Verification is build success + manual browser walkthrough, same as Phase 1.
- Auth approach is custom JWT + `PasswordHasher<User>` from `Microsoft.Extensions.Identity.Core` — explicitly NOT the full `Microsoft.AspNetCore.Identity.EntityFrameworkCore` stack (no `IdentityDbContext`, no `UserManager`/`SignInManager`).
- Controllers contain no business rules; services contain business rules. [10-backend.md]
- Dependency Injection through extension methods. [10-backend.md]
- Fluent API with `IEntityTypeConfiguration` for EF configuration. [10-backend.md]
- One class per file, clear names, no abbreviations, no TODOs, no commented dead code, async/await for I/O-bound work. [30-conventions.md]
- Commit messages follow Conventional Commits. [30-conventions.md]
- `Program.cs` changes are limited to exactly two additions: `builder.Services.AddJwtAuthentication(builder.Configuration);` among the existing `AddX` calls, and `app.UseAuthentication();` inserted immediately before the existing `app.UseAuthorization();`. Nothing else in `Program.cs` reorders (the Task 4 brief also adds a small Development-only auto-migrate block — see that task).
- Do not implement Hotels, Room Types, Rooms, Guests, Reservations, or Dashboard — those are later phases.

---

### Task 1: `User` entity, Fluent API configuration, and migration

**Files:**
- Create: `backend/HotelBookingEngine.Api/Features/Auth/Role.cs`
- Create: `backend/HotelBookingEngine.Api/Features/Auth/User.cs`
- Create: `backend/HotelBookingEngine.Api/Features/Auth/UserConfiguration.cs`
- Modify: `backend/HotelBookingEngine.Api/Persistence/AppDbContext.cs`
- Create: `backend/HotelBookingEngine.Api/Persistence/Migrations/*_AddUsers.cs` (generated)

**Interfaces:**
- Produces: `User { Id, Username, PasswordHash, Role }`, `Role` enum (`Admin`, `Receptionist`), `AppDbContext.Users : DbSet<User>`. Later tasks (`AuthService`, tests) construct `User` via object initializer and query `_dbContext.Users`.

- [ ] **Step 1: Add the password-hashing package**

```bash
dotnet add backend/HotelBookingEngine.Api package Microsoft.Extensions.Identity.Core
```

- [ ] **Step 2: Create the `Role` enum**

```csharp
namespace HotelBookingEngine.Api.Features.Auth;

public enum Role
{
    Admin,
    Receptionist
}
```

Save as `backend/HotelBookingEngine.Api/Features/Auth/Role.cs`.

- [ ] **Step 3: Create the `User` entity**

```csharp
namespace HotelBookingEngine.Api.Features.Auth;

public class User
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public Role Role { get; set; }
}
```

Save as `backend/HotelBookingEngine.Api/Features/Auth/User.cs`.

- [ ] **Step 4: Create the Fluent API configuration with the seed**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelBookingEngine.Api.Features.Auth;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public const string SeedAdminPasswordHash =
        "AQAAAAIAAYagAAAAEFcUQUh++8uhwIPl5ZLbH9pqYcVgsjXD2wd02MD3xXjMRNh2hWOOXD3s/J8LnDygRA==";

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(u => u.Username)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .IsRequired();

        builder.Property(u => u.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasData(new User
        {
            Id = 1,
            Username = "admin",
            PasswordHash = SeedAdminPasswordHash,
            Role = Role.Admin
        });
    }
}
```

Save as `backend/HotelBookingEngine.Api/Features/Auth/UserConfiguration.cs`. The hash above corresponds to the password `Admin123!` — this is the documented development login (record it in Task 8's README update too).

- [ ] **Step 5: Wire `User` into `AppDbContext`**

Replace the contents of `backend/HotelBookingEngine.Api/Persistence/AppDbContext.cs` with:

```csharp
using Microsoft.EntityFrameworkCore;
using HotelBookingEngine.Api.Features.Auth;

namespace HotelBookingEngine.Api.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

Using `ApplyConfigurationsFromAssembly` instead of a manual `ApplyConfiguration(new UserConfiguration())` call means Phase 3+ entities won't require touching this file again — each new `IEntityTypeConfiguration<T>` is picked up automatically.

- [ ] **Step 6: Generate the migration**

```bash
dotnet ef migrations add AddUsers --project backend/HotelBookingEngine.Api --output-dir Persistence/Migrations
```

If `dotnet-ef` isn't found, add `~/.dotnet/tools` to `PATH` for the session (it was installed globally in Phase 1).

- [ ] **Step 7: Verify**

```bash
dotnet build backend/HotelBookingEngine.sln
```

Expected: build succeeds. Open the generated migration file and confirm it creates a `Users` table and inserts the seed row with `Id = 1, Username = "admin"`.

- [ ] **Step 8: Commit**

```bash
git add backend
git commit -m "feat: add User entity with seeded admin account"
```

---

### Task 2: JWT options and token generator (TDD)

**Files:**
- Create: `backend/HotelBookingEngine.Api/Features/Auth/JwtOptions.cs`
- Create: `backend/HotelBookingEngine.Api/Features/Auth/IJwtTokenGenerator.cs`
- Create: `backend/HotelBookingEngine.Api/Features/Auth/JwtTokenGenerator.cs`
- Create: `backend/HotelBookingEngine.Tests/Features/Auth/JwtTokenGeneratorTests.cs`

**Interfaces:**
- Consumes: `User` from Task 1.
- Produces: `IJwtTokenGenerator.GenerateToken(User user) : (string Token, DateTime ExpiresAtUtc)`, `JwtOptions { Issuer, Audience, SigningKey, ExpiryMinutes }` bound from config section `"Jwt"`. Task 3's `AuthService` and Task 4's DI wiring depend on these exact names.

- [ ] **Step 1: Add the JWT package**

```bash
dotnet add backend/HotelBookingEngine.Api package Microsoft.AspNetCore.Authentication.JwtBearer
```

- [ ] **Step 2: Write the failing tests**

```csharp
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
```

Save as `backend/HotelBookingEngine.Tests/Features/Auth/JwtTokenGeneratorTests.cs`.

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test backend/HotelBookingEngine.Tests --filter JwtTokenGeneratorTests`
Expected: FAIL (build error — `JwtOptions`/`JwtTokenGenerator` don't exist yet).

- [ ] **Step 4: Implement `JwtOptions`**

```csharp
namespace HotelBookingEngine.Api.Features.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public required string SigningKey { get; set; }
    public int ExpiryMinutes { get; set; } = 60;
}
```

Save as `backend/HotelBookingEngine.Api/Features/Auth/JwtOptions.cs`.

- [ ] **Step 5: Implement `IJwtTokenGenerator`**

```csharp
namespace HotelBookingEngine.Api.Features.Auth;

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAtUtc) GenerateToken(User user);
}
```

Save as `backend/HotelBookingEngine.Api/Features/Auth/IJwtTokenGenerator.cs`.

- [ ] **Step 6: Implement `JwtTokenGenerator`**

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HotelBookingEngine.Api.Features.Auth;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _options;

    public JwtTokenGenerator(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public (string Token, DateTime ExpiresAtUtc) GenerateToken(User user)
    {
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
```

Save as `backend/HotelBookingEngine.Api/Features/Auth/JwtTokenGenerator.cs`.

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test backend/HotelBookingEngine.Tests --filter JwtTokenGeneratorTests`
Expected: PASS (2 passed).

- [ ] **Step 8: Commit**

```bash
git add backend
git commit -m "feat: add JWT token generator"
```

---

### Task 3: `AuthService` — login and current-user lookup (TDD)

**Files:**
- Create: `backend/HotelBookingEngine.Api/Features/Auth/LoginRequest.cs`
- Create: `backend/HotelBookingEngine.Api/Features/Auth/LoginResponse.cs`
- Create: `backend/HotelBookingEngine.Api/Features/Auth/CurrentUserResponse.cs`
- Create: `backend/HotelBookingEngine.Api/Features/Auth/IAuthService.cs`
- Create: `backend/HotelBookingEngine.Api/Features/Auth/AuthService.cs`
- Create: `backend/HotelBookingEngine.Tests/Features/Auth/AuthServiceTests.cs`

**Interfaces:**
- Consumes: `User`/`AppDbContext` (Task 1), `IJwtTokenGenerator` (Task 2).
- Produces: `IAuthService.LoginAsync(LoginRequest, CancellationToken) : Task<LoginResponse?>` (null on bad credentials), `IAuthService.GetCurrentUser(ClaimsPrincipal) : CurrentUserResponse`. Task 4's `AuthController` calls both by these exact signatures.

- [ ] **Step 1: Write the failing tests**

```csharp
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
```

Save as `backend/HotelBookingEngine.Tests/Features/Auth/AuthServiceTests.cs`. If `Microsoft.Data.Sqlite.SqliteConnection` doesn't resolve, add the package explicitly: `dotnet add backend/HotelBookingEngine.Tests package Microsoft.Data.Sqlite`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/HotelBookingEngine.Tests --filter AuthServiceTests`
Expected: FAIL (build error — `LoginRequest`/`AuthService`/etc. don't exist yet).

- [ ] **Step 3: Implement the DTOs**

```csharp
namespace HotelBookingEngine.Api.Features.Auth;

public class LoginRequest
{
    public required string Username { get; set; }
    public required string Password { get; set; }
}
```

Save as `backend/HotelBookingEngine.Api/Features/Auth/LoginRequest.cs`.

```csharp
namespace HotelBookingEngine.Api.Features.Auth;

public record LoginResponse(string Token, DateTime ExpiresAtUtc, string Username, string Role);
```

Save as `backend/HotelBookingEngine.Api/Features/Auth/LoginResponse.cs`.

```csharp
namespace HotelBookingEngine.Api.Features.Auth;

public record CurrentUserResponse(int Id, string Username, string Role);
```

Save as `backend/HotelBookingEngine.Api/Features/Auth/CurrentUserResponse.cs`.

- [ ] **Step 4: Implement `IAuthService`**

```csharp
using System.Security.Claims;

namespace HotelBookingEngine.Api.Features.Auth;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    CurrentUserResponse GetCurrentUser(ClaimsPrincipal principal);
}
```

Save as `backend/HotelBookingEngine.Api/Features/Auth/IAuthService.cs`.

- [ ] **Step 5: Implement `AuthService`**

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HotelBookingEngine.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HotelBookingEngine.Api.Features.Auth;

public class AuthService : IAuthService
{
    private readonly AppDbContext _dbContext;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AuthService(AppDbContext dbContext, IJwtTokenGenerator tokenGenerator, IPasswordHasher<User> passwordHasher)
    {
        _dbContext = dbContext;
        _tokenGenerator = tokenGenerator;
        _passwordHasher = passwordHasher;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var normalizedUsername = request.Username.ToLowerInvariant();
        var user = await _dbContext.Users
            .SingleOrDefaultAsync(u => u.Username.ToLower() == normalizedUsername, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return null;
        }

        var (token, expiresAtUtc) = _tokenGenerator.GenerateToken(user);
        return new LoginResponse(token, expiresAtUtc, user.Username, user.Role.ToString());
    }

    public CurrentUserResponse GetCurrentUser(ClaimsPrincipal principal)
    {
        var id = int.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var username = principal.FindFirstValue(ClaimTypes.Name)!;
        var role = principal.FindFirstValue(ClaimTypes.Role)!;

        return new CurrentUserResponse(id, username, role);
    }
}
```

Save as `backend/HotelBookingEngine.Api/Features/Auth/AuthService.cs`.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test backend/HotelBookingEngine.Tests --filter AuthServiceTests`
Expected: PASS (4 passed).

- [ ] **Step 7: Run the full suite once**

Run: `dotnet test backend/HotelBookingEngine.sln`
Expected: all tests pass (previous Health/middleware/JWT-generator tests plus these 4).

- [ ] **Step 8: Commit**

```bash
git add backend
git commit -m "feat: add AuthService for login and current-user lookup"
```

---

### Task 4: `AuthController`, JWT wiring, and `Program.cs` assembly

**Files:**
- Create: `backend/HotelBookingEngine.Api/Features/Auth/AuthController.cs`
- Create: `backend/HotelBookingEngine.Api/Extensions/JwtAuthenticationServiceCollectionExtensions.cs`
- Modify: `backend/HotelBookingEngine.Api/Extensions/ApplicationServicesCollectionExtensions.cs`
- Modify: `backend/HotelBookingEngine.Api/appsettings.json`
- Modify: `backend/HotelBookingEngine.Api/appsettings.Development.json`
- Modify: `backend/HotelBookingEngine.Api/Program.cs`

**Interfaces:**
- Consumes: `IAuthService` (Task 3), `JwtOptions` (Task 2).
- Produces: `POST /api/auth/login`, `GET /api/auth/me` — Task 5's integration tests and Task 6/7's frontend both call these exact routes.

- [ ] **Step 1: Implement `AuthController`**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelBookingEngine.Api.Features.Auth;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        return result is null ? Unauthorized() : Ok(result);
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<CurrentUserResponse> Me()
    {
        return Ok(_authService.GetCurrentUser(User));
    }
}
```

Save as `backend/HotelBookingEngine.Api/Features/Auth/AuthController.cs`.

- [ ] **Step 2: Create the JWT authentication extension**

```csharp
using System.Text;
using HotelBookingEngine.Api.Features.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace HotelBookingEngine.Api.Extensions;

public static class JwtAuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Missing 'Jwt' configuration section.");

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey))
                };
            });

        return services;
    }
}
```

Save as `backend/HotelBookingEngine.Api/Extensions/JwtAuthenticationServiceCollectionExtensions.cs`.

- [ ] **Step 3: Register the new services**

Replace the contents of `backend/HotelBookingEngine.Api/Extensions/ApplicationServicesCollectionExtensions.cs` with:

```csharp
using HotelBookingEngine.Api.Features.Auth;
using HotelBookingEngine.Api.Features.Health;
using Microsoft.AspNetCore.Identity;

namespace HotelBookingEngine.Api.Extensions;

public static class ApplicationServicesCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IHealthService, HealthService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        return services;
    }
}
```

- [ ] **Step 4: Add the `Jwt` configuration section**

In `backend/HotelBookingEngine.Api/appsettings.json`, add a `Jwt` key alongside the existing top-level keys:

```json
"Jwt": {
  "Issuer": "HotelBookingEngine",
  "Audience": "HotelBookingEngine.Client",
  "SigningKey": "REPLACE_WITH_A_REAL_SECRET_IN_PRODUCTION_MIN_32_CHARS",
  "ExpiryMinutes": 60
}
```

In `backend/HotelBookingEngine.Api/appsettings.Development.json`, add (overriding only the signing key; `Issuer`/`Audience`/`ExpiryMinutes` are inherited from `appsettings.json`):

```json
"Jwt": {
  "SigningKey": "ThisIsADevelopmentOnlySigningKey1234567890"
}
```

- [ ] **Step 5: Assemble `Program.cs`**

Two additions only. First, add this line among the existing `builder.Services.AddX(...)` calls (after `AddFrontendCorsPolicy`):

```csharp
builder.Services.AddJwtAuthentication(builder.Configuration);
```

Second, insert `app.UseAuthentication();` immediately before the existing `app.UseAuthorization();` line. Third (a Development-only convenience, not a pipeline reorder), extend the existing `if (app.Environment.IsDevelopment())` block that already contains `app.UseSwagger(); app.UseSwaggerUI();` to also apply pending migrations, so a fresh clone works without a manual `dotnet ef database update` step:

```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}
```

This requires one more using at the top of the file: `using HotelBookingEngine.Api.Persistence;` (needed for `AppDbContext`; `CreateScope`/`GetRequiredService` resolve via the Web SDK's implicit `Microsoft.Extensions.DependencyInjection` using).

The full resulting `Program.cs`:

```csharp
using HotelBookingEngine.Api.Extensions;
using HotelBookingEngine.Api.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerWithJwt();
builder.Services.AddPersistence(builder.Configuration, builder.Environment);
builder.Services.AddApplicationServices();
builder.Services.AddFrontendCorsPolicy(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.UseGlobalExceptionHandling();
app.UseHttpsRedirection();
app.UseCors(FrontendCorsServiceCollectionExtensions.FrontendPolicyName);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program
{
}
```

- [ ] **Step 6: Verify end-to-end manually**

```bash
dotnet build backend/HotelBookingEngine.sln
dotnet run --project backend/HotelBookingEngine.Api
```

In another terminal (replace the port with the one from `Properties/launchSettings.json` if different from Phase 1's `5058`):

```bash
curl -i -X POST http://localhost:5058/api/auth/login -H "Content-Type: application/json" -d "{\"username\":\"admin\",\"password\":\"Admin123!\"}"
```

Expected: `200` with a JSON body containing `token`, `expiresAtUtc`, `username: "admin"`, `role: "Admin"`. Copy the `token` value, then:

```bash
curl -i http://localhost:5058/api/auth/me -H "Authorization: Bearer <token>"
```

Expected: `200` with `{"id":1,"username":"admin","role":"Admin"}`. Then without the header:

```bash
curl -i http://localhost:5058/api/auth/me
```

Expected: `401`. Stop the running process once confirmed.

- [ ] **Step 7: Commit**

```bash
git add backend
git commit -m "feat: wire JWT authentication and expose login/me endpoints"
```

---

### Task 5: Integration tests for the auth endpoints (TDD)

**Files:**
- Create: `backend/HotelBookingEngine.Tests/Features/Auth/AuthEndpointsTests.cs`
- Modify: `backend/HotelBookingEngine.Tests/HotelBookingEngine.Tests.csproj` (new package)

**Interfaces:**
- Consumes: `Program` (public partial, from Phase 1), `AppDbContext`, `POST /api/auth/login` and `GET /api/auth/me` (Task 4).

- [ ] **Step 1: Add the testing package**

```bash
dotnet add backend/HotelBookingEngine.Tests package Microsoft.AspNetCore.Mvc.Testing
```

- [ ] **Step 2: Write the tests**

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HotelBookingEngine.Api.Features.Auth;
using HotelBookingEngine.Api.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace HotelBookingEngine.Tests.Features.Auth;

public class AuthEndpointsTests : IDisposable
{
    private readonly string _dbPath;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AuthEndpointsTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"hotelbookingengine-tests-{Guid.NewGuid():N}.db");

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={_dbPath}"));
            });
        });

        using (var scope = _factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
        }

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { Username = "admin", Password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithSeededAdminCredentials_ReturnsTokenAndThenMeSucceeds()
    {
        var loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login", new { Username = "admin", Password = "Admin123!" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);
        Assert.Equal("admin", login!.Username);
        Assert.Equal("Admin", login.Role);

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        var meResponse = await _client.SendAsync(meRequest);

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var me = await meResponse.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.Equal("admin", me!.Username);
        Assert.Equal("Admin", me.Role);
    }

    [Fact]
    public async Task Me_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
```

Save as `backend/HotelBookingEngine.Tests/Features/Auth/AuthEndpointsTests.cs`.

- [ ] **Step 3: Run to verify RED then GREEN**

Run: `dotnet test backend/HotelBookingEngine.Tests --filter AuthEndpointsTests`
This is integration-level (exercises real, already-implemented endpoints), so RED here would only occur if Task 4 was skipped or is broken — confirm it's GREEN on the first run: 3 passed.

- [ ] **Step 4: Run the full suite**

Run: `dotnet test backend/HotelBookingEngine.sln`
Expected: all tests pass, pristine output.

- [ ] **Step 5: Commit**

```bash
git add backend
git commit -m "test: add integration tests for login and me endpoints"
```

---

### Task 6: Frontend auth state — token store, context, service

**Files:**
- Create: `frontend/src/types/auth.ts`
- Create: `frontend/src/stores/tokenStore.ts`
- Create: `frontend/src/features/auth/authService.ts`
- Create: `frontend/src/stores/AuthContext.tsx`
- Create: `frontend/src/hooks/useAuth.ts`
- Modify: `frontend/src/api/httpClient.ts`
- Modify: `frontend/src/main.tsx`

**Interfaces:**
- Consumes: `POST /api/auth/login`, `GET /api/auth/me` (Task 4).
- Produces: `useAuth() : { user: CurrentUser | null, isLoading: boolean, login(username, password): Promise<void>, logout(): void }`. Task 7 (`LoginPage`) and Task 8 (`HomePage`) both consume this hook by this exact shape.

- [ ] **Step 1: Add the auth types**

```typescript
export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  expiresAtUtc: string;
  username: string;
  role: string;
}

export interface CurrentUser {
  id: number;
  username: string;
  role: string;
}
```

Save as `frontend/src/types/auth.ts`.

- [ ] **Step 2: Create the token store**

```typescript
const TOKEN_STORAGE_KEY = "hotelBookingEngine.token";

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_STORAGE_KEY);
}

export function setToken(token: string): void {
  localStorage.setItem(TOKEN_STORAGE_KEY, token);
}

export function clearToken(): void {
  localStorage.removeItem(TOKEN_STORAGE_KEY);
}
```

Save as `frontend/src/stores/tokenStore.ts`.

- [ ] **Step 3: Create the auth service**

```typescript
import { httpClient } from "../../api/httpClient";
import type { CurrentUser, LoginRequest, LoginResponse } from "../../types/auth";

export async function login(credentials: LoginRequest): Promise<LoginResponse> {
  const response = await httpClient.post<LoginResponse>("/api/auth/login", credentials);
  return response.data;
}

export async function fetchCurrentUser(): Promise<CurrentUser> {
  const response = await httpClient.get<CurrentUser>("/api/auth/me");
  return response.data;
}
```

Save as `frontend/src/features/auth/authService.ts`.

- [ ] **Step 4: Add the Axios auth interceptor**

Replace the contents of `frontend/src/api/httpClient.ts` with:

```typescript
import axios from "axios";
import { getToken } from "../stores/tokenStore";

export const httpClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
});

httpClient.interceptors.request.use((config) => {
  const token = getToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});
```

- [ ] **Step 5: Create the auth context**

```typescript
import { createContext, useEffect, useState, type ReactNode } from "react";
import { fetchCurrentUser, login as loginRequest } from "../features/auth/authService";
import { clearToken, getToken, setToken } from "./tokenStore";
import type { CurrentUser } from "../types/auth";

interface AuthContextValue {
  user: CurrentUser | null;
  isLoading: boolean;
  login: (username: string, password: string) => Promise<void>;
  logout: () => void;
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const token = getToken();
    if (!token) {
      setIsLoading(false);
      return;
    }

    fetchCurrentUser()
      .then(setUser)
      .catch(() => clearToken())
      .finally(() => setIsLoading(false));
  }, []);

  async function login(username: string, password: string) {
    const response = await loginRequest({ username, password });
    setToken(response.token);
    const currentUser = await fetchCurrentUser();
    setUser(currentUser);
  }

  function logout() {
    clearToken();
    setUser(null);
  }

  return (
    <AuthContext.Provider value={{ user, isLoading, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}
```

Save as `frontend/src/stores/AuthContext.tsx`.

- [ ] **Step 6: Create the `useAuth` hook**

```typescript
import { useContext } from "react";
import { AuthContext } from "../stores/AuthContext";

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return context;
}
```

Save as `frontend/src/hooks/useAuth.ts`.

- [ ] **Step 7: Wrap the app in `AuthProvider`**

Replace the contents of `frontend/src/main.tsx` with:

```typescript
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { AppRoutes } from "./routes/AppRoutes";
import { AuthProvider } from "./stores/AuthContext";
import "./index.css";

const queryClient = new QueryClient();

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <BrowserRouter>
          <AppRoutes />
        </BrowserRouter>
      </AuthProvider>
    </QueryClientProvider>
  </StrictMode>,
);
```

- [ ] **Step 8: Verify**

```bash
cd frontend && npm run build
```

Expected: 0 TypeScript errors.

- [ ] **Step 9: Commit**

```bash
git add frontend
git commit -m "feat: add frontend auth state (token store, context, service)"
```

---

### Task 7: Login page

**Files:**
- Create: `frontend/src/pages/LoginPage.tsx`
- Modify: `frontend/src/routes/AppRoutes.tsx`
- Modify: `frontend/package.json` (new dependency)

**Interfaces:**
- Consumes: `useAuth()` (Task 6).
- Produces: route `/login`.

- [ ] **Step 1: Install the RHF/Zod resolver bridge**

```bash
cd frontend && npm install @hookform/resolvers
```

- [ ] **Step 2: Create the login page**

```typescript
import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../hooks/useAuth";

const loginSchema = z.object({
  username: z.string().min(1, "Username is required"),
  password: z.string().min(1, "Password is required"),
});

type LoginFormValues = z.infer<typeof loginSchema>;

export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [loginError, setLoginError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
  });

  async function onSubmit(values: LoginFormValues) {
    setLoginError(null);
    try {
      await login(values.username, values.password);
      navigate("/");
    } catch {
      setLoginError("Invalid username or password.");
    }
  }

  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-4">
      <h1 className="text-2xl font-semibold">Login</h1>
      <form onSubmit={handleSubmit(onSubmit)} className="flex w-full max-w-xs flex-col gap-3">
        <div>
          <label htmlFor="username" className="block text-sm font-medium">
            Username
          </label>
          <input
            id="username"
            type="text"
            className="w-full rounded border px-3 py-2"
            {...register("username")}
          />
          {errors.username && <p className="text-sm text-red-600">{errors.username.message}</p>}
        </div>
        <div>
          <label htmlFor="password" className="block text-sm font-medium">
            Password
          </label>
          <input
            id="password"
            type="password"
            className="w-full rounded border px-3 py-2"
            {...register("password")}
          />
          {errors.password && <p className="text-sm text-red-600">{errors.password.message}</p>}
        </div>
        {loginError && <p className="text-sm text-red-600">{loginError}</p>}
        <button
          type="submit"
          disabled={isSubmitting}
          className="rounded bg-blue-600 px-4 py-2 text-white disabled:opacity-50"
        >
          {isSubmitting ? "Signing in..." : "Sign in"}
        </button>
      </form>
    </main>
  );
}
```

Save as `frontend/src/pages/LoginPage.tsx`.

- [ ] **Step 3: Add the route**

Replace the contents of `frontend/src/routes/AppRoutes.tsx` with:

```typescript
import { Routes, Route } from "react-router-dom";
import { HomePage } from "../pages/HomePage";
import { LoginPage } from "../pages/LoginPage";

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<HomePage />} />
      <Route path="/login" element={<LoginPage />} />
    </Routes>
  );
}
```

- [ ] **Step 4: Verify**

```bash
npm run build
```

Expected: 0 TypeScript errors.

- [ ] **Step 5: Commit**

```bash
git add frontend
git commit -m "feat: add login page"
```

---

### Task 8: Home page reflects auth state, manual end-to-end verification, README update

**Files:**
- Modify: `frontend/src/pages/HomePage.tsx`
- Modify: `README.md`

**Interfaces:**
- Consumes: `useAuth()` (Task 6).

- [ ] **Step 1: Update the Home page**

Replace the contents of `frontend/src/pages/HomePage.tsx` with:

```typescript
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { fetchHealthStatus } from "../features/health/healthService";
import { useAuth } from "../hooks/useAuth";

export function HomePage() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ["health"],
    queryFn: fetchHealthStatus,
  });
  const { user, isLoading: isAuthLoading, logout } = useAuth();

  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-2">
      <h1 className="text-2xl font-semibold">Hotel Booking Engine</h1>

      {isLoading && <p>Checking API status...</p>}
      {isError && <p>Unable to reach the API.</p>}
      {data && <p>API status: {data.status}</p>}

      {isAuthLoading ? (
        <p>Checking session...</p>
      ) : user ? (
        <div className="flex flex-col items-center gap-2">
          <p>
            Logged in as {user.username} ({user.role})
          </p>
          <button onClick={logout} className="rounded bg-gray-200 px-4 py-2">
            Log out
          </button>
        </div>
      ) : (
        <Link to="/login" className="text-blue-600 underline">
          Login
        </Link>
      )}
    </main>
  );
}
```

- [ ] **Step 2: Verify the build**

```bash
npm run build
```

Expected: 0 TypeScript errors.

- [ ] **Step 3: Manual end-to-end verification**

Terminal 1: `dotnet run --project backend/HotelBookingEngine.Api` (Development-mode auto-migration from Task 4 creates and seeds the SQLite DB on first run).
Terminal 2: `cd frontend && npm run dev`

In a browser at the printed Vite URL:
1. Home page loads, shows API status, and a "Login" link (not yet authenticated).
2. Click Login, submit wrong credentials → inline "Invalid username or password." error, stays on `/login`.
3. Submit `admin` / `Admin123!` → redirected to `/`, page now shows "Logged in as admin (Admin)" and a "Log out" button.
4. Refresh the page → still shows logged in (token persisted in `localStorage`, validated against `/me` on load).
5. Click "Log out" → reverts to the "Login" link.

Stop both processes once confirmed. This step needs a human/browser to actually execute — describe the result when reporting back rather than assuming it passed.

- [ ] **Step 4: Update the README**

Add a short "Default credentials (development)" note under the existing "Getting Started" section in `README.md`:

```markdown
### Default Credentials (Development)

The database is seeded with one admin account on first run:

- Username: `admin`
- Password: `Admin123!`
```

Also update the "Status" section to: `Phase 1 and Phase 2 (Authentication) complete. Next: Phase 3 — Hotels.`

- [ ] **Step 5: Commit**

```bash
git add frontend README.md
git commit -m "feat: reflect auth state on Home page and document dev credentials"
```

---

## Self-Review Notes

- **Spec coverage:** Login (Tasks 3-4, 7), JWT issuance/validation (Tasks 2, 4), Role Authorization demonstrated via `/me` + `[Authorize]` (Tasks 3-5) — all three MVP-scope Authentication bullets from `prompts/project-01.md` are covered. Every design-doc decision (roles, seeding, credential field, no `ProtectedRoute`, no refresh tokens, no frontend tests, case-insensitive username lookup) has a corresponding task or explicit constraint above. Hotels/Rooms/Guests/Reservations/Dashboard are untouched, as required.
- **Placeholder scan:** no TODO/TBD; all code blocks are complete; the seed password hash is a real, pre-verified value, not a placeholder to compute later.
- **Type consistency:** `LoginResponse` (C#: `Token, ExpiresAtUtc, Username, Role`) matches TS `LoginResponse` (`token, expiresAtUtc, username, role`) via ASP.NET Core's default camelCase policy — same pattern Phase 1 established for `HealthStatus`. `CurrentUserResponse`/`CurrentUser` likewise. `IAuthService`, `IJwtTokenGenerator`, `JwtOptions` names are used identically across Tasks 2-5.
