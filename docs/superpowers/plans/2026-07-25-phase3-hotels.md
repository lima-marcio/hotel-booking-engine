# Phase 3 - Hotels Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add full Hotels CRUD (Create, Update, Delete, List) to the Hotel Booking Engine, backend and frontend, with write operations restricted to the `Admin` role — the first phase where role-based authorization is actually enforced end-to-end, and the first admin-only page in the frontend.

**Architecture:** A `Features/Hotels/` vertical slice mirrors the shape already established by `Features/Auth/`: entity + Fluent API configuration, a shared request DTO + a response DTO (manual mapping, no AutoMapper), a service holding all logic, and a thin controller. `GET` endpoints require only authentication; `POST`/`PUT`/`DELETE` additionally require `Authorize(Roles="Admin")`. On the frontend, a new `ProtectedRoute` component (deferred from Phase 2 for exactly this moment) gates `/hotels` behind login and `/hotels/new`/`/hotels/:id/edit` behind the `Admin` role; a list page and a shared create/edit form page consume the new endpoints via TanStack Query.

**Tech Stack:** No new backend packages (EF Core, `Microsoft.AspNetCore.Authorization`, `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.Data.Sqlite` already present from Phases 1-2). No new frontend packages (React Router, TanStack Query, React Hook Form, Zod, `@hookform/resolvers` already present from Phase 2).

**Design spec:** `docs/superpowers/specs/2026-07-25-phase3-hotels-design.md` — read for full rationale; this plan implements it as-is.

## Global Constraints

- `Hotel` fields: `Name`, `Address`, `City`, `Phone` — all required. `Name` max length 200, `Address` 300, `City` 100, `Phone` 20. No uniqueness constraint on `Name`.
- Delete is a hard delete (no soft-delete flag, no cascade concerns yet — nothing references `Hotel` in this phase).
- Authorization split by verb: `GET /api/hotels` and `GET /api/hotels/{id}` require only `[Authorize]` (any authenticated role); `POST /api/hotels`, `PUT /api/hotels/{id}`, `DELETE /api/hotels/{id}` require `[Authorize(Roles="Admin")]`.
- `GET /api/hotels/{id}` exists to support the Update flow (edit form needs to load current values) even though the spec's bullet list only says Create/Update/Delete/List.
- No pagination on List.
- Single shared `HotelRequest` DTO for both Create and Update (identical shape); `HotelResponse` for output. Manual mapping in `HotelService`, no AutoMapper.
- `Update`/`Delete`/`GetById` on an unknown id return `404`; `Create` never 404s.
- `ProtectedRoute` (frontend) accepts an optional `roles` prop: absent = "must be logged in"; present = "must be logged in AND have one of these roles." No `roles` → redirect unauthenticated users to `/login`. With `roles` and a role mismatch → redirect to `/`.
- `/hotels` route requires only login; `/hotels/new` and `/hotels/:id/edit` require the `Admin` role. Within `HotelsPage`, write-action buttons (New/Edit/Delete) render only when `user.role === "Admin"`.
- Frontend Zod validation max-lengths mirror the backend exactly (200/300/100/20) so client-side errors match what the API would reject.
- Delete confirmation uses `window.confirm` — no custom modal.
- Testing the Receptionist-forbidden path requires a Receptionist test user; insert one directly into the isolated integration-test database (not into the real migration seed — only `admin` is seeded in production/dev, per Phase 2's decision).
- Controllers contain no business rules; services contain business rules. [10-backend.md]
- Dependency Injection through extension methods. [10-backend.md]
- Fluent API with `IEntityTypeConfiguration`. [10-backend.md]
- One class per file, clear names, no abbreviations, no TODOs, no commented dead code, async/await for I/O-bound work. [30-conventions.md]
- Commit messages follow Conventional Commits. [30-conventions.md]
- Never hardcode data in the frontend; consume only the backend API. [prompts/project-01.md]
- Do not implement Room Types, Rooms, Guests, Reservations, or Dashboard — those are later phases.

---

### Task 1: `Hotel` entity, Fluent API configuration, and migration

**Files:**
- Create: `backend/HotelBookingEngine.Api/Features/Hotels/Hotel.cs`
- Create: `backend/HotelBookingEngine.Api/Features/Hotels/HotelConfiguration.cs`
- Modify: `backend/HotelBookingEngine.Api/Persistence/AppDbContext.cs`
- Create: `backend/HotelBookingEngine.Api/Persistence/Migrations/*_AddHotels.cs` (generated)

**Interfaces:**
- Produces: `Hotel { Id, Name, Address, City, Phone }`, `AppDbContext.Hotels : DbSet<Hotel>`. Task 2's `HotelService` constructs/queries `Hotel` via this DbSet.

- [ ] **Step 1: Create the `Hotel` entity**

```csharp
namespace HotelBookingEngine.Api.Features.Hotels;

public class Hotel
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Address { get; set; }
    public required string City { get; set; }
    public required string Phone { get; set; }
}
```

Save as `backend/HotelBookingEngine.Api/Features/Hotels/Hotel.cs`.

- [ ] **Step 2: Create the Fluent API configuration**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelBookingEngine.Api.Features.Hotels;

public class HotelConfiguration : IEntityTypeConfiguration<Hotel>
{
    public void Configure(EntityTypeBuilder<Hotel> builder)
    {
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(h => h.Address)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(h => h.City)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(h => h.Phone)
            .IsRequired()
            .HasMaxLength(20);
    }
}
```

Save as `backend/HotelBookingEngine.Api/Features/Hotels/HotelConfiguration.cs`. No `HasData` seed — hotels are created through the app, not pre-populated.

- [ ] **Step 3: Wire `Hotel` into `AppDbContext`**

Add one line to `backend/HotelBookingEngine.Api/Persistence/AppDbContext.cs`: a `using HotelBookingEngine.Api.Features.Hotels;` at the top, and `public DbSet<Hotel> Hotels => Set<Hotel>();` alongside the existing `Users` property. `OnModelCreating`'s `ApplyConfigurationsFromAssembly` already picks up the new `HotelConfiguration` automatically — no change needed there.

Resulting file:

```csharp
using Microsoft.EntityFrameworkCore;
using HotelBookingEngine.Api.Features.Auth;
using HotelBookingEngine.Api.Features.Hotels;

namespace HotelBookingEngine.Api.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Hotel> Hotels => Set<Hotel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

- [ ] **Step 4: Generate the migration**

```bash
dotnet ef migrations add AddHotels --project backend/HotelBookingEngine.Api --output-dir Persistence/Migrations
```

If `dotnet-ef` isn't found, add `~/.dotnet/tools` to `PATH` for the session.

- [ ] **Step 5: Verify**

```bash
dotnet build backend/HotelBookingEngine.sln
```

Expected: build succeeds. Open the generated migration and confirm it creates only a `Hotels` table (no seed data, unlike `AddUsers`).

- [ ] **Step 6: Commit**

```bash
git add backend
git commit -m "feat: add Hotel entity and migration"
```

---

### Task 2: `HotelService` — CRUD logic (TDD)

**Files:**
- Create: `backend/HotelBookingEngine.Api/Features/Hotels/HotelRequest.cs`
- Create: `backend/HotelBookingEngine.Api/Features/Hotels/HotelResponse.cs`
- Create: `backend/HotelBookingEngine.Api/Features/Hotels/IHotelService.cs`
- Create: `backend/HotelBookingEngine.Api/Features/Hotels/HotelService.cs`
- Create: `backend/HotelBookingEngine.Tests/Features/Hotels/HotelServiceTests.cs`

**Interfaces:**
- Consumes: `Hotel`/`AppDbContext` (Task 1).
- Produces: `IHotelService.CreateAsync(HotelRequest, CancellationToken) : Task<HotelResponse>`, `UpdateAsync(int, HotelRequest, CancellationToken) : Task<HotelResponse?>`, `DeleteAsync(int, CancellationToken) : Task<bool>`, `GetByIdAsync(int, CancellationToken) : Task<HotelResponse?>`, `ListAsync(CancellationToken) : Task<List<HotelResponse>>`. Task 3's `HotelsController` calls these by these exact signatures.

- [ ] **Step 1: Write the failing tests**

```csharp
using HotelBookingEngine.Api.Features.Hotels;
using HotelBookingEngine.Api.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelBookingEngine.Tests.Features.Hotels;

public class HotelServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _dbContext;
    private readonly HotelService _sut;

    public HotelServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();

        _sut = new HotelService(_dbContext);
    }

    private static HotelRequest SampleRequest(string name = "Grand Hotel") => new()
    {
        Name = name,
        Address = "123 Main St",
        City = "Springfield",
        Phone = "555-0100"
    };

    [Fact]
    public async Task CreateAsync_PersistsAndReturnsHotel()
    {
        var result = await _sut.CreateAsync(SampleRequest(), CancellationToken.None);

        Assert.True(result.Id > 0);
        Assert.Equal("Grand Hotel", result.Name);

        var stored = await _dbContext.Hotels.FindAsync(result.Id);
        Assert.NotNull(stored);
        Assert.Equal("Grand Hotel", stored!.Name);
    }

    [Fact]
    public async Task UpdateAsync_WithExistingId_UpdatesAndReturnsHotel()
    {
        var created = await _sut.CreateAsync(SampleRequest(), CancellationToken.None);

        var updated = await _sut.UpdateAsync(
            created.Id, SampleRequest("Renamed Hotel"), CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("Renamed Hotel", updated!.Name);
    }

    [Fact]
    public async Task UpdateAsync_WithUnknownId_ReturnsNull()
    {
        var result = await _sut.UpdateAsync(999, SampleRequest(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_WithExistingId_RemovesHotelAndReturnsTrue()
    {
        var created = await _sut.CreateAsync(SampleRequest(), CancellationToken.None);

        var result = await _sut.DeleteAsync(created.Id, CancellationToken.None);

        Assert.True(result);
        Assert.Null(await _dbContext.Hotels.FindAsync(created.Id));
    }

    [Fact]
    public async Task DeleteAsync_WithUnknownId_ReturnsFalse()
    {
        var result = await _sut.DeleteAsync(999, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task ListAsync_ReturnsAllCreatedHotels()
    {
        await _sut.CreateAsync(SampleRequest("Hotel A"), CancellationToken.None);
        await _sut.CreateAsync(SampleRequest("Hotel B"), CancellationToken.None);

        var result = await _sut.ListAsync(CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, h => h.Name == "Hotel A");
        Assert.Contains(result, h => h.Name == "Hotel B");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }
}
```

Save as `backend/HotelBookingEngine.Tests/Features/Hotels/HotelServiceTests.cs`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/HotelBookingEngine.Tests --filter HotelServiceTests`
Expected: FAIL (build error — `HotelRequest`/`HotelService`/etc. don't exist yet).

- [ ] **Step 3: Implement the DTOs**

```csharp
namespace HotelBookingEngine.Api.Features.Hotels;

public class HotelRequest
{
    public required string Name { get; set; }
    public required string Address { get; set; }
    public required string City { get; set; }
    public required string Phone { get; set; }
}
```

Save as `backend/HotelBookingEngine.Api/Features/Hotels/HotelRequest.cs`.

```csharp
namespace HotelBookingEngine.Api.Features.Hotels;

public record HotelResponse(int Id, string Name, string Address, string City, string Phone);
```

Save as `backend/HotelBookingEngine.Api/Features/Hotels/HotelResponse.cs`.

- [ ] **Step 4: Implement `IHotelService`**

```csharp
namespace HotelBookingEngine.Api.Features.Hotels;

public interface IHotelService
{
    Task<HotelResponse> CreateAsync(HotelRequest request, CancellationToken cancellationToken);
    Task<HotelResponse?> UpdateAsync(int id, HotelRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
    Task<HotelResponse?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<List<HotelResponse>> ListAsync(CancellationToken cancellationToken);
}
```

Save as `backend/HotelBookingEngine.Api/Features/Hotels/IHotelService.cs`.

- [ ] **Step 5: Implement `HotelService`**

```csharp
using HotelBookingEngine.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HotelBookingEngine.Api.Features.Hotels;

public class HotelService : IHotelService
{
    private readonly AppDbContext _dbContext;

    public HotelService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HotelResponse> CreateAsync(HotelRequest request, CancellationToken cancellationToken)
    {
        var hotel = new Hotel
        {
            Name = request.Name,
            Address = request.Address,
            City = request.City,
            Phone = request.Phone
        };

        _dbContext.Hotels.Add(hotel);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(hotel);
    }

    public async Task<HotelResponse?> UpdateAsync(int id, HotelRequest request, CancellationToken cancellationToken)
    {
        var hotel = await _dbContext.Hotels.FindAsync([id], cancellationToken);
        if (hotel is null)
        {
            return null;
        }

        hotel.Name = request.Name;
        hotel.Address = request.Address;
        hotel.City = request.City;
        hotel.Phone = request.Phone;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(hotel);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var hotel = await _dbContext.Hotels.FindAsync([id], cancellationToken);
        if (hotel is null)
        {
            return false;
        }

        _dbContext.Hotels.Remove(hotel);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<HotelResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var hotel = await _dbContext.Hotels.FindAsync([id], cancellationToken);
        return hotel is null ? null : ToResponse(hotel);
    }

    public async Task<List<HotelResponse>> ListAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Hotels
            .OrderBy(h => h.Name)
            .Select(h => new HotelResponse(h.Id, h.Name, h.Address, h.City, h.Phone))
            .ToListAsync(cancellationToken);
    }

    private static HotelResponse ToResponse(Hotel hotel) =>
        new(hotel.Id, hotel.Name, hotel.Address, hotel.City, hotel.Phone);
}
```

Save as `backend/HotelBookingEngine.Api/Features/Hotels/HotelService.cs`.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test backend/HotelBookingEngine.Tests --filter HotelServiceTests`
Expected: PASS (6 passed).

- [ ] **Step 7: Run the full suite**

Run: `dotnet test backend/HotelBookingEngine.sln`
Expected: all tests pass (Phase 1/2 tests plus these 6).

- [ ] **Step 8: Commit**

```bash
git add backend
git commit -m "feat: add HotelService for hotel CRUD"
```

---

### Task 3: `HotelsController` and DI registration

**Files:**
- Create: `backend/HotelBookingEngine.Api/Features/Hotels/HotelsController.cs`
- Modify: `backend/HotelBookingEngine.Api/Extensions/ApplicationServicesCollectionExtensions.cs`

**Interfaces:**
- Consumes: `IHotelService` (Task 2).
- Produces: `GET /api/hotels`, `GET /api/hotels/{id}`, `POST /api/hotels`, `PUT /api/hotels/{id}`, `DELETE /api/hotels/{id}` — Task 4's integration tests and Tasks 6-8's frontend both call these exact routes.

- [ ] **Step 1: Implement `HotelsController`**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelBookingEngine.Api.Features.Hotels;

[ApiController]
[Route("api/hotels")]
[Authorize]
public class HotelsController : ControllerBase
{
    private readonly IHotelService _hotelService;

    public HotelsController(IHotelService hotelService)
    {
        _hotelService = hotelService;
    }

    [HttpGet]
    public async Task<ActionResult<List<HotelResponse>>> List(CancellationToken cancellationToken)
    {
        return Ok(await _hotelService.ListAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<HotelResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var hotel = await _hotelService.GetByIdAsync(id, cancellationToken);
        return hotel is null ? NotFound() : Ok(hotel);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<HotelResponse>> Create(HotelRequest request, CancellationToken cancellationToken)
    {
        var hotel = await _hotelService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = hotel.Id }, hotel);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<HotelResponse>> Update(int id, HotelRequest request, CancellationToken cancellationToken)
    {
        var hotel = await _hotelService.UpdateAsync(id, request, cancellationToken);
        return hotel is null ? NotFound() : Ok(hotel);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _hotelService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
```

Save as `backend/HotelBookingEngine.Api/Features/Hotels/HotelsController.cs`. The class-level `[Authorize]` plus the method-level `[Authorize(Roles = "Admin")]` on the three write actions combine with AND semantics in ASP.NET Core (both requirements must pass) — this is exactly "any authenticated user can read, only Admin can write," no custom policy needed.

- [ ] **Step 2: Register `IHotelService`**

Replace the contents of `backend/HotelBookingEngine.Api/Extensions/ApplicationServicesCollectionExtensions.cs` with:

```csharp
using HotelBookingEngine.Api.Features.Auth;
using HotelBookingEngine.Api.Features.Health;
using HotelBookingEngine.Api.Features.Hotels;
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
        services.AddScoped<IHotelService, HotelService>();

        return services;
    }
}
```

- [ ] **Step 3: Verify manually**

```bash
dotnet build backend/HotelBookingEngine.sln
dotnet run --project backend/HotelBookingEngine.Api
```

In another terminal (port from `Properties/launchSettings.json`, `5058` unless changed):

```bash
curl -s -X POST http://localhost:5058/api/auth/login -H "Content-Type: application/json" -d "{\"username\":\"admin\",\"password\":\"Admin123!\"}"
```

Copy the `token`, then:

```bash
curl -i -X POST http://localhost:5058/api/hotels -H "Content-Type: application/json" -H "Authorization: Bearer <token>" -d "{\"name\":\"Grand Hotel\",\"address\":\"123 Main St\",\"city\":\"Springfield\",\"phone\":\"555-0100\"}"
```

Expected: `201 Created` with the hotel body including an `id`. Then:

```bash
curl -i http://localhost:5058/api/hotels -H "Authorization: Bearer <token>"
```

Expected: `200` with a JSON array containing the created hotel. Then without a token:

```bash
curl -i http://localhost:5058/api/hotels
```

Expected: `401`. (Role-based `403` for a non-Admin user is proven in Task 4 — no Receptionist account exists to test manually yet.) Stop the process once confirmed.

- [ ] **Step 4: Commit**

```bash
git add backend
git commit -m "feat: add HotelsController with role-restricted write endpoints"
```

---

### Task 4: Integration tests proving role enforcement

**Files:**
- Create: `backend/HotelBookingEngine.Tests/Features/Hotels/HotelsEndpointsTests.cs`

**Interfaces:**
- Consumes: `Program`, `AppDbContext`, `User`/`Role` (Task 1 of Phase 2), `POST /api/auth/login`, all `/api/hotels*` routes (Task 3).

- [ ] **Step 1: Write the tests**

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HotelBookingEngine.Api.Features.Auth;
using HotelBookingEngine.Api.Features.Hotels;
using HotelBookingEngine.Api.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace HotelBookingEngine.Tests.Features.Hotels;

public class HotelsEndpointsTests : IDisposable
{
    private const string ReceptionistUsername = "receptionist-test";
    private const string ReceptionistPassword = "Reception123!";

    private readonly string _dbPath;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public HotelsEndpointsTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"hotelbookingengine-hotels-tests-{Guid.NewGuid():N}.db");

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
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Database.Migrate();

            var passwordHasher = new PasswordHasher<User>();
            dbContext.Users.Add(new User
            {
                Username = ReceptionistUsername,
                PasswordHash = passwordHasher.HashPassword(null!, ReceptionistPassword),
                Role = Role.Receptionist
            });
            dbContext.SaveChanges();
        }

        _client = _factory.CreateClient();
    }

    private async Task<string> LoginAsync(string username, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { Username = username, Password = password });
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.Token;
    }

    private void AuthorizeAs(string token)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static object SampleRequestBody(string name = "Grand Hotel") => new
    {
        Name = name,
        Address = "123 Main St",
        City = "Springfield",
        Phone = "555-0100"
    };

    [Fact]
    public async Task List_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/hotels");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_AsReceptionist_ReturnsOk()
    {
        AuthorizeAs(await LoginAsync(ReceptionistUsername, ReceptionistPassword));

        var response = await _client.GetAsync("/api/hotels");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsReceptionist_ReturnsForbidden()
    {
        AuthorizeAs(await LoginAsync(ReceptionistUsername, ReceptionistPassword));

        var response = await _client.PostAsJsonAsync("/api/hotels", SampleRequestBody());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsAdmin_ReturnsCreatedAndThenListIncludesIt()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));

        var createResponse = await _client.PostAsJsonAsync("/api/hotels", SampleRequestBody());
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<HotelResponse>();
        Assert.NotNull(created);
        Assert.Equal("Grand Hotel", created!.Name);

        var listResponse = await _client.GetAsync("/api/hotels");
        var hotels = await listResponse.Content.ReadFromJsonAsync<List<HotelResponse>>();
        Assert.Contains(hotels!, h => h.Id == created.Id);
    }

    [Fact]
    public async Task Update_AsReceptionist_ReturnsForbidden()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var created = await (await _client.PostAsJsonAsync("/api/hotels", SampleRequestBody()))
            .Content.ReadFromJsonAsync<HotelResponse>();

        AuthorizeAs(await LoginAsync(ReceptionistUsername, ReceptionistPassword));
        var response = await _client.PutAsJsonAsync($"/api/hotels/{created!.Id}", SampleRequestBody("Updated Name"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsReceptionist_ReturnsForbidden()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var created = await (await _client.PostAsJsonAsync("/api/hotels", SampleRequestBody()))
            .Content.ReadFromJsonAsync<HotelResponse>();

        AuthorizeAs(await LoginAsync(ReceptionistUsername, ReceptionistPassword));
        var response = await _client.DeleteAsync($"/api/hotels/{created!.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsAdmin_RemovesHotel()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var created = await (await _client.PostAsJsonAsync("/api/hotels", SampleRequestBody()))
            .Content.ReadFromJsonAsync<HotelResponse>();

        var deleteResponse = await _client.DeleteAsync($"/api/hotels/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/api/hotels/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        SqliteConnection.ClearAllPools();

        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
```

Save as `backend/HotelBookingEngine.Tests/Features/Hotels/HotelsEndpointsTests.cs`. The `SqliteConnection.ClearAllPools()` call in `Dispose()` mirrors the Windows file-lock workaround already established in `AuthEndpointsTests` (Phase 2) — reuse the same pattern, don't invent a different one.

- [ ] **Step 2: Run the tests**

Run: `dotnet test backend/HotelBookingEngine.Tests --filter HotelsEndpointsTests`
Expected: PASS (7 passed). This is integration-level testing of already-implemented functionality (Task 3), so GREEN on the first run is expected — there's no new production code to write here.

- [ ] **Step 3: Run the full suite**

Run: `dotnet test backend/HotelBookingEngine.sln`
Expected: all tests pass, pristine output.

- [ ] **Step 4: Commit**

```bash
git add backend
git commit -m "test: add integration tests for hotel endpoint role authorization"
```

---

### Task 5: `ProtectedRoute` frontend component

**Files:**
- Create: `frontend/src/routes/ProtectedRoute.tsx`

**Interfaces:**
- Consumes: `useAuth()` (Phase 2).
- Produces: `<ProtectedRoute roles?: string[]>{children}</ProtectedRoute>`. Task 7/8 wrap `HotelsPage`/`HotelFormPage` routes with this.

- [ ] **Step 1: Create the component**

```typescript
import { Navigate } from "react-router-dom";
import type { ReactNode } from "react";
import { useAuth } from "../hooks/useAuth";

interface ProtectedRouteProps {
  children: ReactNode;
  roles?: string[];
}

export function ProtectedRoute({ children, roles }: ProtectedRouteProps) {
  const { user, isLoading } = useAuth();

  if (isLoading) {
    return <p>Checking session...</p>;
  }

  if (!user) {
    return <Navigate to="/login" replace />;
  }

  if (roles && !roles.includes(user.role)) {
    return <Navigate to="/" replace />;
  }

  return <>{children}</>;
}
```

Save as `frontend/src/routes/ProtectedRoute.tsx`. Not wired into any route yet in this task — Tasks 7-8 do that once there are pages to protect.

- [ ] **Step 2: Verify**

```bash
cd frontend && npm run build
```

Expected: 0 TypeScript errors (the component compiles standalone even though nothing imports it yet).

- [ ] **Step 3: Commit**

```bash
git add frontend
git commit -m "feat: add ProtectedRoute component"
```

---

### Task 6: Hotel types and frontend API service

**Files:**
- Create: `frontend/src/types/hotel.ts`
- Create: `frontend/src/features/hotels/hotelService.ts`

**Interfaces:**
- Produces: `Hotel { id, name, address, city, phone }`, `HotelRequest { name, address, city, phone }`, `listHotels()`, `getHotel(id)`, `createHotel()`, `updateHotel(id)`, `deleteHotel(id)`. Tasks 7-8 consume these.

- [ ] **Step 1: Create the types**

```typescript
export interface Hotel {
  id: number;
  name: string;
  address: string;
  city: string;
  phone: string;
}

export interface HotelRequest {
  name: string;
  address: string;
  city: string;
  phone: string;
}
```

Save as `frontend/src/types/hotel.ts`.

- [ ] **Step 2: Create the API service**

```typescript
import { httpClient } from "../../api/httpClient";
import type { Hotel, HotelRequest } from "../../types/hotel";

export async function listHotels(): Promise<Hotel[]> {
  const response = await httpClient.get<Hotel[]>("/api/hotels");
  return response.data;
}

export async function getHotel(id: number): Promise<Hotel> {
  const response = await httpClient.get<Hotel>(`/api/hotels/${id}`);
  return response.data;
}

export async function createHotel(request: HotelRequest): Promise<Hotel> {
  const response = await httpClient.post<Hotel>("/api/hotels", request);
  return response.data;
}

export async function updateHotel(id: number, request: HotelRequest): Promise<Hotel> {
  const response = await httpClient.put<Hotel>(`/api/hotels/${id}`, request);
  return response.data;
}

export async function deleteHotel(id: number): Promise<void> {
  await httpClient.delete(`/api/hotels/${id}`);
}
```

Save as `frontend/src/features/hotels/hotelService.ts`.

- [ ] **Step 3: Verify**

```bash
npm run build
```

Expected: 0 TypeScript errors.

- [ ] **Step 4: Commit**

```bash
git add frontend
git commit -m "feat: add hotel types and API service"
```

---

### Task 7: Hotels list page

**Files:**
- Create: `frontend/src/pages/HotelsPage.tsx`
- Modify: `frontend/src/routes/AppRoutes.tsx`
- Modify: `frontend/src/pages/HomePage.tsx`

**Interfaces:**
- Consumes: `listHotels`, `deleteHotel` (Task 6), `ProtectedRoute` (Task 5), `useAuth()` (Phase 2).
- Produces: route `/hotels`.

- [ ] **Step 1: Create the Hotels list page**

```typescript
import { Link } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { deleteHotel, listHotels } from "../features/hotels/hotelService";
import { useAuth } from "../hooks/useAuth";

export function HotelsPage() {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const { data, isLoading, isError } = useQuery({
    queryKey: ["hotels"],
    queryFn: listHotels,
  });

  const deleteMutation = useMutation({
    mutationFn: deleteHotel,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["hotels"] });
    },
  });

  const isAdmin = user?.role === "Admin";

  function handleDelete(id: number, name: string) {
    if (window.confirm(`Delete "${name}"?`)) {
      deleteMutation.mutate(id);
    }
  }

  return (
    <main className="flex min-h-screen flex-col items-center gap-4 p-8">
      <h1 className="text-2xl font-semibold">Hotels</h1>

      {isAdmin && (
        <Link to="/hotels/new" className="text-blue-600 underline">
          New Hotel
        </Link>
      )}

      {isLoading && <p>Loading hotels...</p>}
      {isError && <p>Unable to load hotels.</p>}

      {data && (
        <table className="w-full max-w-3xl border-collapse">
          <thead>
            <tr className="border-b text-left">
              <th className="p-2">Name</th>
              <th className="p-2">Address</th>
              <th className="p-2">City</th>
              <th className="p-2">Phone</th>
              {isAdmin && <th className="p-2">Actions</th>}
            </tr>
          </thead>
          <tbody>
            {data.map((hotel) => (
              <tr key={hotel.id} className="border-b">
                <td className="p-2">{hotel.name}</td>
                <td className="p-2">{hotel.address}</td>
                <td className="p-2">{hotel.city}</td>
                <td className="p-2">{hotel.phone}</td>
                {isAdmin && (
                  <td className="flex gap-2 p-2">
                    <Link to={`/hotels/${hotel.id}/edit`} className="text-blue-600 underline">
                      Edit
                    </Link>
                    <button
                      onClick={() => handleDelete(hotel.id, hotel.name)}
                      className="text-red-600 underline"
                    >
                      Delete
                    </button>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </main>
  );
}
```

Save as `frontend/src/pages/HotelsPage.tsx`.

- [ ] **Step 2: Add the `/hotels` route**

Replace the contents of `frontend/src/routes/AppRoutes.tsx` with:

```typescript
import { Routes, Route } from "react-router-dom";
import { HomePage } from "../pages/HomePage";
import { LoginPage } from "../pages/LoginPage";
import { HotelsPage } from "../pages/HotelsPage";
import { ProtectedRoute } from "./ProtectedRoute";

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<HomePage />} />
      <Route path="/login" element={<LoginPage />} />
      <Route
        path="/hotels"
        element={
          <ProtectedRoute>
            <HotelsPage />
          </ProtectedRoute>
        }
      />
    </Routes>
  );
}
```

- [ ] **Step 3: Add a "Hotels" link to the Home page**

In `frontend/src/pages/HomePage.tsx`, inside the `user ?` branch (the logged-in state), add a link to `/hotels` between the "Logged in as..." paragraph and the Logout button:

```typescript
{isAuthLoading ? (
  <p>Checking session...</p>
) : user ? (
  <div className="flex flex-col items-center gap-2">
    <p>
      Logged in as {user.username} ({user.role})
    </p>
    <Link to="/hotels" className="text-blue-600 underline">
      Hotels
    </Link>
    <button onClick={logout} className="rounded bg-gray-200 px-4 py-2">
      Log out
    </button>
  </div>
) : (
  <Link to="/login" className="text-blue-600 underline">
    Login
  </Link>
)}
```

(`Link` is already imported in this file from Phase 2.)

- [ ] **Step 4: Verify**

```bash
npm run build
```

Expected: 0 TypeScript errors.

- [ ] **Step 5: Commit**

```bash
git add frontend
git commit -m "feat: add hotels list page"
```

---

### Task 8: Hotel create/edit form, remaining routes, manual verification, README

**Files:**
- Create: `frontend/src/pages/HotelFormPage.tsx`
- Modify: `frontend/src/routes/AppRoutes.tsx`
- Modify: `README.md`

**Interfaces:**
- Consumes: `createHotel`, `updateHotel`, `getHotel` (Task 6), `ProtectedRoute` (Task 5).
- Produces: routes `/hotels/new`, `/hotels/:id/edit`.

- [ ] **Step 1: Create the Hotel form page**

```typescript
import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useNavigate, useParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createHotel, getHotel, updateHotel } from "../features/hotels/hotelService";

const hotelSchema = z.object({
  name: z.string().min(1, "Name is required").max(200),
  address: z.string().min(1, "Address is required").max(300),
  city: z.string().min(1, "City is required").max(100),
  phone: z.string().min(1, "Phone is required").max(20),
});

type HotelFormValues = z.infer<typeof hotelSchema>;

export function HotelFormPage() {
  const { id } = useParams<{ id: string }>();
  const hotelId = id ? Number(id) : undefined;
  const isEditMode = hotelId !== undefined;
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const { data: existingHotel } = useQuery({
    queryKey: ["hotels", hotelId],
    queryFn: () => getHotel(hotelId!),
    enabled: isEditMode,
  });

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<HotelFormValues>({
    resolver: zodResolver(hotelSchema),
  });

  useEffect(() => {
    if (existingHotel) {
      reset({
        name: existingHotel.name,
        address: existingHotel.address,
        city: existingHotel.city,
        phone: existingHotel.phone,
      });
    }
  }, [existingHotel, reset]);

  const mutation = useMutation({
    mutationFn: (values: HotelFormValues) =>
      isEditMode ? updateHotel(hotelId!, values) : createHotel(values),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["hotels"] });
      navigate("/hotels");
    },
  });

  function onSubmit(values: HotelFormValues) {
    mutation.mutate(values);
  }

  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-4">
      <h1 className="text-2xl font-semibold">{isEditMode ? "Edit Hotel" : "New Hotel"}</h1>
      <form onSubmit={handleSubmit(onSubmit)} className="flex w-full max-w-sm flex-col gap-3">
        <div>
          <label htmlFor="name" className="block text-sm font-medium">
            Name
          </label>
          <input id="name" type="text" className="w-full rounded border px-3 py-2" {...register("name")} />
          {errors.name && <p className="text-sm text-red-600">{errors.name.message}</p>}
        </div>
        <div>
          <label htmlFor="address" className="block text-sm font-medium">
            Address
          </label>
          <input id="address" type="text" className="w-full rounded border px-3 py-2" {...register("address")} />
          {errors.address && <p className="text-sm text-red-600">{errors.address.message}</p>}
        </div>
        <div>
          <label htmlFor="city" className="block text-sm font-medium">
            City
          </label>
          <input id="city" type="text" className="w-full rounded border px-3 py-2" {...register("city")} />
          {errors.city && <p className="text-sm text-red-600">{errors.city.message}</p>}
        </div>
        <div>
          <label htmlFor="phone" className="block text-sm font-medium">
            Phone
          </label>
          <input id="phone" type="text" className="w-full rounded border px-3 py-2" {...register("phone")} />
          {errors.phone && <p className="text-sm text-red-600">{errors.phone.message}</p>}
        </div>
        <button
          type="submit"
          disabled={isSubmitting}
          className="rounded bg-blue-600 px-4 py-2 text-white disabled:opacity-50"
        >
          {isSubmitting ? "Saving..." : "Save"}
        </button>
      </form>
    </main>
  );
}
```

Save as `frontend/src/pages/HotelFormPage.tsx`.

- [ ] **Step 2: Add the remaining routes**

Replace the contents of `frontend/src/routes/AppRoutes.tsx` with:

```typescript
import { Routes, Route } from "react-router-dom";
import { HomePage } from "../pages/HomePage";
import { LoginPage } from "../pages/LoginPage";
import { HotelsPage } from "../pages/HotelsPage";
import { HotelFormPage } from "../pages/HotelFormPage";
import { ProtectedRoute } from "./ProtectedRoute";

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<HomePage />} />
      <Route path="/login" element={<LoginPage />} />
      <Route
        path="/hotels"
        element={
          <ProtectedRoute>
            <HotelsPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/hotels/new"
        element={
          <ProtectedRoute roles={["Admin"]}>
            <HotelFormPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/hotels/:id/edit"
        element={
          <ProtectedRoute roles={["Admin"]}>
            <HotelFormPage />
          </ProtectedRoute>
        }
      />
    </Routes>
  );
}
```

- [ ] **Step 3: Verify the build**

```bash
npm run build
```

Expected: 0 TypeScript errors.

- [ ] **Step 4: Manual end-to-end verification**

Terminal 1: `dotnet run --project backend/HotelBookingEngine.Api`
Terminal 2: `cd frontend && npm run dev`

In a browser:
1. Log in as `admin`/`Admin123!`. Home now shows a "Hotels" link.
2. Click it → `/hotels` loads, empty list (or whatever was left from Task 3's curl test), with a "New Hotel" link visible (Admin).
3. Click "New Hotel" → fill the form → Save → redirected to `/hotels`, the new hotel appears in the table.
4. Click "Edit" on that row → form pre-filled with current values → change the name → Save → redirected back, table shows the updated name.
5. Click "Delete" → confirm dialog → row disappears from the table.
6. Navigate directly to `/hotels/new` while logged out (open a private/incognito window, or log out first) → redirected to `/login`.
7. (Optional, requires creating a second account manually since there's no UI for it) Confirm a Receptionist-role user sees the `/hotels` table without "New Hotel"/"Edit"/"Delete" controls, and is redirected away from `/hotels/new` if navigated to directly. This exact enforcement is already proven by Task 4's integration tests at the API level; the frontend gating logic (`isAdmin` checks, `ProtectedRoute roles`) is straightforward enough to verify by code inspection if no second account is available to click through with.

Stop both processes once confirmed. This step needs a human/browser to actually execute — describe the result when reporting back rather than assuming it passed.

- [ ] **Step 5: Update the README**

Update the "Status" line in `README.md` to: `Phase 1, Phase 2 (Authentication), and Phase 3 (Hotels) complete. Next: Phase 4 — Room Types.`

- [ ] **Step 6: Commit**

```bash
git add frontend README.md
git commit -m "feat: add hotel create/edit form and wire remaining routes"
```

---

## Self-Review Notes

- **Spec coverage:** Create/Update/Delete/List Hotel (Tasks 1-3, 7-8) and role-based authorization enforced end-to-end (Tasks 3-4) — both MVP-scope Hotels bullets and the "Role Authorization" capability (only proven at the claim level in Phase 2) are now covered. Every design-doc decision (fields, no unique-name constraint, hard delete, verb-split authorization, `GetById` addition, no pagination, shared `HotelRequest`, `ProtectedRoute` now, route-level + in-page role gating, `window.confirm`, test-only Receptionist user, matching Zod max-lengths, delete-invalidates-list) has a corresponding task or explicit constraint above. Room Types/Rooms/Guests/Reservations/Dashboard are untouched, as required.
- **Placeholder scan:** no TODO/TBD; all code blocks are complete and consistent with the established Phase 1/2 patterns (SQLite-backed unit tests, `WebApplicationFactory` integration tests, manual DTO mapping, thin controllers).
- **Type consistency:** `HotelResponse` (C#: `Id, Name, Address, City, Phone`) matches TS `Hotel` (`id, name, address, city, phone`) via ASP.NET Core's default camelCase policy, same pattern as `LoginResponse`/`HealthStatus`. `HotelRequest`/`IHotelService` names are used identically across Tasks 2-4; `ProtectedRoute`'s `roles` prop shape matches how Task 8 invokes it (`roles={["Admin"]}`, comparing against `user.role` which is the same string `Role.ToString()` produces server-side — `"Admin"`/`"Receptionist"`).
