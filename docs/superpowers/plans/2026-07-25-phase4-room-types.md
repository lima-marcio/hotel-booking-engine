# Phase 4 - Room Types Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add full Room Types CRUD (Create, Update, Delete, List) to the Hotel Booking Engine, backend and frontend. Room Types are the first child entity in the data model — each belongs to a specific `Hotel` — and the first time deleting one entity (`Hotel`) must consider another (`RoomType`).

**Architecture:** A `Features/RoomTypes/` vertical slice mirrors `Features/Hotels/` exactly: entity + Fluent API configuration, a shared request DTO + response DTO, a service, and a thin controller. Because `RoomType` belongs to a `Hotel`, the service's `CreateAsync`/`ListByHotelAsync` return `null` to mean "the hotel doesn't exist" (→ `404`), distinct from an empty list. Routing splits across two URL shapes on one controller: hotel-scoped list/create (`/api/hotels/{hotelId}/room-types`) and flat get/update/delete by id (`/api/room-types/{id}`). `IHotelService.DeleteAsync`'s contract changes from `Task<bool>` to `Task<HotelDeleteResult>` so `HotelsController` can distinguish "not found" from "blocked because room types still reference this hotel" and return `409` instead of silently cascading. The frontend adds a hotel-scoped list page and a shared create/edit form page, both gated by the existing `ProtectedRoute`, plus an `onError` handler on `HotelsPage`'s delete mutation to surface that new `409`.

**Tech Stack:** No new backend packages (EF Core, `Microsoft.AspNetCore.Authorization`, `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.Data.Sqlite` already present). No new frontend packages (React Router, TanStack Query, React Hook Form, Zod, `@hookform/resolvers`, Axios already present).

**Design spec:** `docs/superpowers/specs/2026-07-25-phase4-room-types-design.md` — read for full rationale; this plan implements it as-is.

## Global Constraints

- `RoomType` fields: `HotelId` (required FK → `Hotel.Id`), `Name` (required, max length 100), `Description` (required, max length 500), `Capacity` (required int, must be > 0), `DailyRate` (required decimal, must be > 0). `Capacity` and `DailyRate` live on `RoomType`, not `Room` — a deliberate deviation from the spec's literal field grouping, recorded in the design doc.
- No EF-level `OnDelete` cascade behavior is configured on the `RoomType` → `Hotel` relationship. The Restrict-on-delete rule is enforced in `HotelService`, at the application layer, before EF ever attempts the hotel delete — so the database-level default FK behavior never actually triggers.
- `IHotelService.DeleteAsync` changes from `Task<bool>` to `Task<HotelDeleteResult>` where `HotelDeleteResult` is `{ Deleted, NotFound, HasRoomTypes }`. `HotelsController.Delete` maps `Deleted → 204`, `NotFound → 404`, `HasRoomTypes → 409`.
- Routing: `GET`/`POST /api/hotels/{hotelId}/room-types` (list/create, hotel-scoped); `GET`/`PUT`/`DELETE /api/room-types/{id}` (flat, id is already globally unique). One controller (`RoomTypesController`), no class-level `[Route]` — each action has its own absolute route template since the two URL shapes coexist.
- `RoomTypeService.CreateAsync`/`ListByHotelAsync` return `null` specifically to mean "the hotel doesn't exist" (→ `404` in the controller), distinct from an empty list (a real hotel with zero room types yet → `200` with `[]`). Matches the existing `null`-means-not-found convention from `HotelService`.
- Authorization mirrors Hotels exactly: `GET` requires only `[Authorize]` (any role); `POST`/`PUT`/`DELETE` require `[Authorize(Roles="Admin")]` in addition.
- Single shared `RoomTypeRequest` DTO for both Create and Update (no `HotelId` on it — that comes from the route); `RoomTypeResponse` includes `HotelId`. Manual mapping in `RoomTypeService`, no AutoMapper.
- Frontend routes nest under the hotel: `/hotels/:hotelId/room-types`, `/hotels/:hotelId/room-types/new`, `/hotels/:hotelId/room-types/:id/edit`. `HotelsPage` gains a "Room Types" link per row, visible to any authenticated user (reads are open, matching the API).
- `HotelsPage`'s delete mutation gains an `onError` handler that surfaces the backend's new `409` message. `HotelRoomTypesPage`'s delete mutation does **not** get the same treatment in this phase — nothing yet references `RoomType`, so its only failure mode is a generic error with no specific message to surface. Revisit in Phase 5 when `Room` references `RoomType`.
- Frontend Zod validation mirrors the backend exactly: `name` max 100, `description` max 500, `capacity` a positive integer, `dailyRate` a positive number.
- Delete confirmation uses `window.confirm` — no custom modal, matching `HotelsPage`.
- Submit buttons disable via `mutation.isPending` while a create/update request is in flight (the pattern already fixed on `HotelFormPage` in Phase 3 review) — not via React Hook Form's `isSubmitting`.
- Controllers contain no business rules; services contain business rules. [10-backend.md]
- Dependency Injection through extension methods. [10-backend.md]
- Fluent API with `IEntityTypeConfiguration`. [10-backend.md]
- One class per file, clear names, no abbreviations, no TODOs, no commented dead code, async/await for I/O-bound work. [30-conventions.md]
- Commit messages follow Conventional Commits. [30-conventions.md]
- Never hardcode data in the frontend; consume only the backend API. [prompts/project-01.md]
- Do not implement Rooms, Guests, Reservations, or Dashboard — those are later phases. Do not build any UI for viewing room types across all hotels at once, or for re-parenting a room type to a different hotel.

---

### Task 1: `RoomType` entity, Fluent API configuration, and migration

**Files:**
- Create: `backend/HotelBookingEngine.Api/Features/RoomTypes/RoomType.cs`
- Create: `backend/HotelBookingEngine.Api/Features/RoomTypes/RoomTypeConfiguration.cs`
- Modify: `backend/HotelBookingEngine.Api/Persistence/AppDbContext.cs`
- Create: `backend/HotelBookingEngine.Api/Persistence/Migrations/*_AddRoomTypes.cs` (generated)

**Interfaces:**
- Consumes: `Hotel` (Phase 3, `Features/Hotels/Hotel.cs`) — the FK target.
- Produces: `RoomType { Id, HotelId, Name, Description, Capacity, DailyRate }`, `AppDbContext.RoomTypes : DbSet<RoomType>`. Task 2's `HotelService` queries this DbSet for the Restrict-on-delete check; Task 4's `RoomTypeService` constructs/queries `RoomType` via this DbSet.

- [ ] **Step 1: Create the `RoomType` entity**

```csharp
namespace HotelBookingEngine.Api.Features.RoomTypes;

public class RoomType
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public int Capacity { get; set; }
    public decimal DailyRate { get; set; }
}
```

Save as `backend/HotelBookingEngine.Api/Features/RoomTypes/RoomType.cs`.

- [ ] **Step 2: Create the Fluent API configuration**

```csharp
using HotelBookingEngine.Api.Features.Hotels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelBookingEngine.Api.Features.RoomTypes;

public class RoomTypeConfiguration : IEntityTypeConfiguration<RoomType>
{
    public void Configure(EntityTypeBuilder<RoomType> builder)
    {
        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(rt => rt.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(rt => rt.Capacity)
            .IsRequired();

        builder.Property(rt => rt.DailyRate)
            .IsRequired();

        builder.HasOne<Hotel>()
            .WithMany()
            .HasForeignKey(rt => rt.HotelId)
            .IsRequired();
    }
}
```

Save as `backend/HotelBookingEngine.Api/Features/RoomTypes/RoomTypeConfiguration.cs`. No `HasData` seed. No explicit `OnDelete(...)` call — the Restrict behavior is enforced in `HotelService` (Task 2), not at the EF/database level, per the design doc.

- [ ] **Step 3: Wire `RoomType` into `AppDbContext`**

Replace the contents of `backend/HotelBookingEngine.Api/Persistence/AppDbContext.cs` with:

```csharp
using Microsoft.EntityFrameworkCore;
using HotelBookingEngine.Api.Features.Auth;
using HotelBookingEngine.Api.Features.Hotels;
using HotelBookingEngine.Api.Features.RoomTypes;

namespace HotelBookingEngine.Api.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Hotel> Hotels => Set<Hotel>();
    public DbSet<RoomType> RoomTypes => Set<RoomType>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

- [ ] **Step 4: Generate the migration**

```bash
dotnet ef migrations add AddRoomTypes --project backend/HotelBookingEngine.Api --output-dir Persistence/Migrations
```

- [ ] **Step 5: Verify**

```bash
dotnet build backend/HotelBookingEngine.sln
```

Expected: build succeeds. Open the generated migration and confirm it creates a `RoomTypes` table with a foreign key to `Hotels` and no seed data.

- [ ] **Step 6: Commit**

```bash
git add backend
git commit -m "feat: add RoomType entity and migration"
```

---

### Task 2: `HotelService.DeleteAsync` → `HotelDeleteResult` contract change (TDD)

**Files:**
- Create: `backend/HotelBookingEngine.Api/Features/Hotels/HotelDeleteResult.cs`
- Modify: `backend/HotelBookingEngine.Api/Features/Hotels/IHotelService.cs`
- Modify: `backend/HotelBookingEngine.Api/Features/Hotels/HotelService.cs`
- Modify: `backend/HotelBookingEngine.Tests/Features/Hotels/HotelServiceTests.cs`

**Interfaces:**
- Consumes: `AppDbContext.RoomTypes` (Task 1).
- Produces: `HotelDeleteResult { Deleted, NotFound, HasRoomTypes }`, `IHotelService.DeleteAsync(int, CancellationToken) : Task<HotelDeleteResult>` (was `Task<bool>`). Task 3's `HotelsController.Delete` maps these three outcomes to HTTP status codes.

- [ ] **Step 1: Update the failing/changed tests**

Replace the two delete tests and add a new one in `backend/HotelBookingEngine.Tests/Features/Hotels/HotelServiceTests.cs`. Add `using HotelBookingEngine.Api.Features.RoomTypes;` to the top of the file (alongside the existing usings), then replace:

```csharp
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
```

with:

```csharp
    [Fact]
    public async Task DeleteAsync_WithExistingId_RemovesHotelAndReturnsDeleted()
    {
        var created = await _sut.CreateAsync(SampleRequest(), CancellationToken.None);

        var result = await _sut.DeleteAsync(created.Id, CancellationToken.None);

        Assert.Equal(HotelDeleteResult.Deleted, result);
        Assert.Null(await _dbContext.Hotels.FindAsync(created.Id));
    }

    [Fact]
    public async Task DeleteAsync_WithUnknownId_ReturnsNotFound()
    {
        var result = await _sut.DeleteAsync(999, CancellationToken.None);

        Assert.Equal(HotelDeleteResult.NotFound, result);
    }

    [Fact]
    public async Task DeleteAsync_WithExistingRoomTypes_ReturnsHasRoomTypesAndDoesNotDelete()
    {
        var created = await _sut.CreateAsync(SampleRequest(), CancellationToken.None);
        _dbContext.RoomTypes.Add(new RoomType
        {
            HotelId = created.Id,
            Name = "Deluxe",
            Description = "Spacious room with a view",
            Capacity = 2,
            DailyRate = 150m
        });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.DeleteAsync(created.Id, CancellationToken.None);

        Assert.Equal(HotelDeleteResult.HasRoomTypes, result);
        Assert.NotNull(await _dbContext.Hotels.FindAsync(created.Id));
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/HotelBookingEngine.Tests --filter HotelServiceTests`
Expected: FAIL (build error — `HotelDeleteResult` doesn't exist yet, and `DeleteAsync` still returns `bool`).

- [ ] **Step 3: Add the `HotelDeleteResult` enum**

```csharp
namespace HotelBookingEngine.Api.Features.Hotels;

public enum HotelDeleteResult
{
    Deleted,
    NotFound,
    HasRoomTypes
}
```

Save as `backend/HotelBookingEngine.Api/Features/Hotels/HotelDeleteResult.cs`.

- [ ] **Step 4: Update `IHotelService`**

```csharp
namespace HotelBookingEngine.Api.Features.Hotels;

public interface IHotelService
{
    Task<HotelResponse> CreateAsync(HotelRequest request, CancellationToken cancellationToken);
    Task<HotelResponse?> UpdateAsync(int id, HotelRequest request, CancellationToken cancellationToken);
    Task<HotelDeleteResult> DeleteAsync(int id, CancellationToken cancellationToken);
    Task<HotelResponse?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<List<HotelResponse>> ListAsync(CancellationToken cancellationToken);
}
```

Save as `backend/HotelBookingEngine.Api/Features/Hotels/IHotelService.cs`.

- [ ] **Step 5: Update `HotelService.DeleteAsync`**

In `backend/HotelBookingEngine.Api/Features/Hotels/HotelService.cs`, replace:

```csharp
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
```

with:

```csharp
    public async Task<HotelDeleteResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var hotel = await _dbContext.Hotels.FindAsync([id], cancellationToken);
        if (hotel is null)
        {
            return HotelDeleteResult.NotFound;
        }

        var hasRoomTypes = await _dbContext.RoomTypes.AnyAsync(rt => rt.HotelId == id, cancellationToken);
        if (hasRoomTypes)
        {
            return HotelDeleteResult.HasRoomTypes;
        }

        _dbContext.Hotels.Remove(hotel);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return HotelDeleteResult.Deleted;
    }
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test backend/HotelBookingEngine.Tests --filter HotelServiceTests`
Expected: PASS (7 passed — the 6 original tests renamed/updated plus the new `HasRoomTypes` test).

- [ ] **Step 7: Commit**

```bash
git add backend
git commit -m "feat: change HotelService.DeleteAsync to return HotelDeleteResult"
```

---

### Task 3: `HotelsController.Delete` mapping and the `409` integration test

**Files:**
- Modify: `backend/HotelBookingEngine.Api/Features/Hotels/HotelsController.cs`
- Modify: `backend/HotelBookingEngine.Tests/Features/Hotels/HotelsEndpointsTests.cs`

**Interfaces:**
- Consumes: `HotelDeleteResult` (Task 2).
- Produces: `DELETE /api/hotels/{id}` now returns `204`/`404`/`409`. Task 8's frontend `onError` handler on the hotel delete mutation depends on the `409` existing.

- [ ] **Step 1: Update `HotelsController.Delete`**

In `backend/HotelBookingEngine.Api/Features/Hotels/HotelsController.cs`, replace:

```csharp
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _hotelService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
```

with:

```csharp
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _hotelService.DeleteAsync(id, cancellationToken);
        return result switch
        {
            HotelDeleteResult.Deleted => NoContent(),
            HotelDeleteResult.NotFound => NotFound(),
            HotelDeleteResult.HasRoomTypes => Conflict("Cannot delete a hotel that still has room types. Delete its room types first."),
            _ => throw new InvalidOperationException($"Unhandled {nameof(HotelDeleteResult)} value: {result}")
        };
    }
```

- [ ] **Step 2: Add the `409` integration test**

Add `using HotelBookingEngine.Api.Features.RoomTypes;` to the top of `backend/HotelBookingEngine.Tests/Features/Hotels/HotelsEndpointsTests.cs`, then add this test (e.g. after `Delete_AsAdmin_RemovesHotel`):

```csharp
    [Fact]
    public async Task Delete_WithExistingRoomType_ReturnsConflict()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var created = await (await _client.PostAsJsonAsync("/api/hotels", SampleRequestBody()))
            .Content.ReadFromJsonAsync<HotelResponse>();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.RoomTypes.Add(new RoomType
            {
                HotelId = created!.Id,
                Name = "Deluxe",
                Description = "Spacious room with a view",
                Capacity = 2,
                DailyRate = 150m
            });
            dbContext.SaveChanges();
        }

        var response = await _client.DeleteAsync($"/api/hotels/{created!.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var getResponse = await _client.GetAsync($"/api/hotels/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }
```

- [ ] **Step 3: Run the tests**

Run: `dotnet test backend/HotelBookingEngine.Tests --filter HotelsEndpointsTests`
Expected: PASS (8 passed).

- [ ] **Step 4: Run the full suite**

Run: `dotnet test backend/HotelBookingEngine.sln`
Expected: all tests pass, pristine output.

- [ ] **Step 5: Commit**

```bash
git add backend
git commit -m "feat: return 409 when deleting a hotel that still has room types"
```

---

### Task 4: `RoomTypeService` — CRUD and hotel-scoping logic (TDD)

**Files:**
- Create: `backend/HotelBookingEngine.Api/Features/RoomTypes/RoomTypeRequest.cs`
- Create: `backend/HotelBookingEngine.Api/Features/RoomTypes/RoomTypeResponse.cs`
- Create: `backend/HotelBookingEngine.Api/Features/RoomTypes/IRoomTypeService.cs`
- Create: `backend/HotelBookingEngine.Api/Features/RoomTypes/RoomTypeService.cs`
- Create: `backend/HotelBookingEngine.Tests/Features/RoomTypes/RoomTypeServiceTests.cs`

**Interfaces:**
- Consumes: `RoomType`/`AppDbContext` (Task 1), `Hotel` (Phase 3).
- Produces: `IRoomTypeService.CreateAsync(int, RoomTypeRequest, CancellationToken) : Task<RoomTypeResponse?>`, `UpdateAsync(int, RoomTypeRequest, CancellationToken) : Task<RoomTypeResponse?>`, `DeleteAsync(int, CancellationToken) : Task<bool>`, `GetByIdAsync(int, CancellationToken) : Task<RoomTypeResponse?>`, `ListByHotelAsync(int, CancellationToken) : Task<List<RoomTypeResponse>?>`. Task 5's `RoomTypesController` calls these by these exact signatures.

- [ ] **Step 1: Write the failing tests**

```csharp
using HotelBookingEngine.Api.Features.Hotels;
using HotelBookingEngine.Api.Features.RoomTypes;
using HotelBookingEngine.Api.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelBookingEngine.Tests.Features.RoomTypes;

public class RoomTypeServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _dbContext;
    private readonly RoomTypeService _sut;

    public RoomTypeServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();

        _sut = new RoomTypeService(_dbContext);
    }

    private async Task<int> CreateHotelAsync(string name = "Grand Hotel")
    {
        var hotel = new Hotel { Name = name, Address = "123 Main St", City = "Springfield", Phone = "555-0100" };
        _dbContext.Hotels.Add(hotel);
        await _dbContext.SaveChangesAsync();
        return hotel.Id;
    }

    private static RoomTypeRequest SampleRequest(string name = "Deluxe") => new()
    {
        Name = name,
        Description = "Spacious room with a view",
        Capacity = 2,
        DailyRate = 150m
    };

    [Fact]
    public async Task CreateAsync_WithExistingHotel_PersistsAndReturnsRoomType()
    {
        var hotelId = await CreateHotelAsync();

        var result = await _sut.CreateAsync(hotelId, SampleRequest(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.Id > 0);
        Assert.Equal(hotelId, result.HotelId);
        Assert.Equal("Deluxe", result.Name);

        var stored = await _dbContext.RoomTypes.FindAsync(result.Id);
        Assert.NotNull(stored);
    }

    [Fact]
    public async Task CreateAsync_WithUnknownHotelId_ReturnsNull()
    {
        var result = await _sut.CreateAsync(999, SampleRequest(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_WithExistingId_UpdatesAndReturnsRoomType()
    {
        var hotelId = await CreateHotelAsync();
        var created = await _sut.CreateAsync(hotelId, SampleRequest(), CancellationToken.None);

        var updated = await _sut.UpdateAsync(created!.Id, SampleRequest("Suite"), CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("Suite", updated!.Name);
    }

    [Fact]
    public async Task UpdateAsync_WithUnknownId_ReturnsNull()
    {
        var result = await _sut.UpdateAsync(999, SampleRequest(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_WithExistingId_RemovesRoomTypeAndReturnsTrue()
    {
        var hotelId = await CreateHotelAsync();
        var created = await _sut.CreateAsync(hotelId, SampleRequest(), CancellationToken.None);

        var result = await _sut.DeleteAsync(created!.Id, CancellationToken.None);

        Assert.True(result);
        Assert.Null(await _dbContext.RoomTypes.FindAsync(created.Id));
    }

    [Fact]
    public async Task DeleteAsync_WithUnknownId_ReturnsFalse()
    {
        var result = await _sut.DeleteAsync(999, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsRoomType()
    {
        var hotelId = await CreateHotelAsync();
        var created = await _sut.CreateAsync(hotelId, SampleRequest(), CancellationToken.None);

        var result = await _sut.GetByIdAsync(created!.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Deluxe", result!.Name);
    }

    [Fact]
    public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
    {
        var result = await _sut.GetByIdAsync(999, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ListByHotelAsync_ReturnsOnlyThatHotelsRoomTypes()
    {
        var hotelAId = await CreateHotelAsync("Hotel A");
        var hotelBId = await CreateHotelAsync("Hotel B");

        await _sut.CreateAsync(hotelAId, SampleRequest("Standard"), CancellationToken.None);
        await _sut.CreateAsync(hotelAId, SampleRequest("Deluxe"), CancellationToken.None);
        await _sut.CreateAsync(hotelBId, SampleRequest("Suite"), CancellationToken.None);

        var result = await _sut.ListByHotelAsync(hotelAId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        Assert.All(result, rt => Assert.Equal(hotelAId, rt.HotelId));
    }

    [Fact]
    public async Task ListByHotelAsync_WithUnknownHotelId_ReturnsNull()
    {
        var result = await _sut.ListByHotelAsync(999, CancellationToken.None);

        Assert.Null(result);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }
}
```

Save as `backend/HotelBookingEngine.Tests/Features/RoomTypes/RoomTypeServiceTests.cs`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/HotelBookingEngine.Tests --filter RoomTypeServiceTests`
Expected: FAIL (build error — `RoomTypeRequest`/`RoomTypeService`/etc. don't exist yet).

- [ ] **Step 3: Implement the DTOs**

```csharp
namespace HotelBookingEngine.Api.Features.RoomTypes;

public class RoomTypeRequest
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public int Capacity { get; set; }
    public decimal DailyRate { get; set; }
}
```

Save as `backend/HotelBookingEngine.Api/Features/RoomTypes/RoomTypeRequest.cs`.

```csharp
namespace HotelBookingEngine.Api.Features.RoomTypes;

public record RoomTypeResponse(int Id, int HotelId, string Name, string Description, int Capacity, decimal DailyRate);
```

Save as `backend/HotelBookingEngine.Api/Features/RoomTypes/RoomTypeResponse.cs`.

- [ ] **Step 4: Implement `IRoomTypeService`**

```csharp
namespace HotelBookingEngine.Api.Features.RoomTypes;

public interface IRoomTypeService
{
    Task<RoomTypeResponse?> CreateAsync(int hotelId, RoomTypeRequest request, CancellationToken cancellationToken);
    Task<RoomTypeResponse?> UpdateAsync(int id, RoomTypeRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
    Task<RoomTypeResponse?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<List<RoomTypeResponse>?> ListByHotelAsync(int hotelId, CancellationToken cancellationToken);
}
```

Save as `backend/HotelBookingEngine.Api/Features/RoomTypes/IRoomTypeService.cs`.

- [ ] **Step 5: Implement `RoomTypeService`**

```csharp
using HotelBookingEngine.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HotelBookingEngine.Api.Features.RoomTypes;

public class RoomTypeService : IRoomTypeService
{
    private readonly AppDbContext _dbContext;

    public RoomTypeService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RoomTypeResponse?> CreateAsync(int hotelId, RoomTypeRequest request, CancellationToken cancellationToken)
    {
        var hotelExists = await _dbContext.Hotels.AnyAsync(h => h.Id == hotelId, cancellationToken);
        if (!hotelExists)
        {
            return null;
        }

        var roomType = new RoomType
        {
            HotelId = hotelId,
            Name = request.Name,
            Description = request.Description,
            Capacity = request.Capacity,
            DailyRate = request.DailyRate
        };

        _dbContext.RoomTypes.Add(roomType);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(roomType);
    }

    public async Task<RoomTypeResponse?> UpdateAsync(int id, RoomTypeRequest request, CancellationToken cancellationToken)
    {
        var roomType = await _dbContext.RoomTypes.FindAsync([id], cancellationToken);
        if (roomType is null)
        {
            return null;
        }

        roomType.Name = request.Name;
        roomType.Description = request.Description;
        roomType.Capacity = request.Capacity;
        roomType.DailyRate = request.DailyRate;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(roomType);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var roomType = await _dbContext.RoomTypes.FindAsync([id], cancellationToken);
        if (roomType is null)
        {
            return false;
        }

        _dbContext.RoomTypes.Remove(roomType);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<RoomTypeResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var roomType = await _dbContext.RoomTypes.FindAsync([id], cancellationToken);
        return roomType is null ? null : ToResponse(roomType);
    }

    public async Task<List<RoomTypeResponse>?> ListByHotelAsync(int hotelId, CancellationToken cancellationToken)
    {
        var hotelExists = await _dbContext.Hotels.AnyAsync(h => h.Id == hotelId, cancellationToken);
        if (!hotelExists)
        {
            return null;
        }

        return await _dbContext.RoomTypes
            .Where(rt => rt.HotelId == hotelId)
            .OrderBy(rt => rt.Name)
            .Select(rt => new RoomTypeResponse(rt.Id, rt.HotelId, rt.Name, rt.Description, rt.Capacity, rt.DailyRate))
            .ToListAsync(cancellationToken);
    }

    private static RoomTypeResponse ToResponse(RoomType roomType) =>
        new(roomType.Id, roomType.HotelId, roomType.Name, roomType.Description, roomType.Capacity, roomType.DailyRate);
}
```

Save as `backend/HotelBookingEngine.Api/Features/RoomTypes/RoomTypeService.cs`.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test backend/HotelBookingEngine.Tests --filter RoomTypeServiceTests`
Expected: PASS (10 passed).

- [ ] **Step 7: Run the full suite**

Run: `dotnet test backend/HotelBookingEngine.sln`
Expected: all tests pass.

- [ ] **Step 8: Commit**

```bash
git add backend
git commit -m "feat: add RoomTypeService for room type CRUD"
```

---

### Task 5: `RoomTypesController` and DI registration

**Files:**
- Create: `backend/HotelBookingEngine.Api/Features/RoomTypes/RoomTypesController.cs`
- Modify: `backend/HotelBookingEngine.Api/Extensions/ApplicationServicesCollectionExtensions.cs`

**Interfaces:**
- Consumes: `IRoomTypeService` (Task 4).
- Produces: `GET`/`POST /api/hotels/{hotelId}/room-types`, `GET`/`PUT`/`DELETE /api/room-types/{id}` — Task 6's integration tests and Tasks 7-9's frontend both call these exact routes.

- [ ] **Step 1: Implement `RoomTypesController`**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelBookingEngine.Api.Features.RoomTypes;

[ApiController]
[Authorize]
public class RoomTypesController : ControllerBase
{
    private readonly IRoomTypeService _roomTypeService;

    public RoomTypesController(IRoomTypeService roomTypeService)
    {
        _roomTypeService = roomTypeService;
    }

    [HttpGet("api/hotels/{hotelId:int}/room-types")]
    public async Task<ActionResult<List<RoomTypeResponse>>> ListByHotel(int hotelId, CancellationToken cancellationToken)
    {
        var roomTypes = await _roomTypeService.ListByHotelAsync(hotelId, cancellationToken);
        return roomTypes is null ? NotFound() : Ok(roomTypes);
    }

    [HttpPost("api/hotels/{hotelId:int}/room-types")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RoomTypeResponse>> Create(int hotelId, RoomTypeRequest request, CancellationToken cancellationToken)
    {
        var roomType = await _roomTypeService.CreateAsync(hotelId, request, cancellationToken);
        return roomType is null ? NotFound() : CreatedAtAction(nameof(GetById), new { id = roomType.Id }, roomType);
    }

    [HttpGet("api/room-types/{id:int}")]
    public async Task<ActionResult<RoomTypeResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var roomType = await _roomTypeService.GetByIdAsync(id, cancellationToken);
        return roomType is null ? NotFound() : Ok(roomType);
    }

    [HttpPut("api/room-types/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RoomTypeResponse>> Update(int id, RoomTypeRequest request, CancellationToken cancellationToken)
    {
        var roomType = await _roomTypeService.UpdateAsync(id, request, cancellationToken);
        return roomType is null ? NotFound() : Ok(roomType);
    }

    [HttpDelete("api/room-types/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _roomTypeService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
```

Save as `backend/HotelBookingEngine.Api/Features/RoomTypes/RoomTypesController.cs`. No class-level `[Route]` — the two URL shapes (`api/hotels/{hotelId}/room-types` and `api/room-types/{id}`) coexist on absolute per-action route templates instead.

- [ ] **Step 2: Register `IRoomTypeService`**

Replace the contents of `backend/HotelBookingEngine.Api/Extensions/ApplicationServicesCollectionExtensions.cs` with:

```csharp
using HotelBookingEngine.Api.Features.Auth;
using HotelBookingEngine.Api.Features.Health;
using HotelBookingEngine.Api.Features.Hotels;
using HotelBookingEngine.Api.Features.RoomTypes;
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
        services.AddScoped<IRoomTypeService, RoomTypeService>();

        return services;
    }
}
```

- [ ] **Step 3: Verify manually**

```bash
dotnet build backend/HotelBookingEngine.sln
dotnet run --project backend/HotelBookingEngine.Api
```

In another terminal (port `5058` unless changed):

```bash
curl -s -X POST http://localhost:5058/api/auth/login -H "Content-Type: application/json" -d "{\"username\":\"admin\",\"password\":\"Admin123!\"}"
```

Copy the `token`, create a hotel, note its `id`, then:

```bash
curl -i -X POST http://localhost:5058/api/hotels/1/room-types -H "Content-Type: application/json" -H "Authorization: Bearer <token>" -d "{\"name\":\"Deluxe\",\"description\":\"Spacious room with a view\",\"capacity\":2,\"dailyRate\":150}"
```

Expected: `201 Created` with the room type body including an `id` and `hotelId`. Then:

```bash
curl -i http://localhost:5058/api/hotels/1/room-types -H "Authorization: Bearer <token>"
```

Expected: `200` with a JSON array containing the created room type. Then:

```bash
curl -i http://localhost:5058/api/hotels/999/room-types -H "Authorization: Bearer <token>"
```

Expected: `404` (unknown hotel id). Stop the process once confirmed.

- [ ] **Step 4: Commit**

```bash
git add backend
git commit -m "feat: add RoomTypesController with role-restricted write endpoints"
```

---

### Task 6: Integration tests proving role enforcement and hotel-scoping

**Files:**
- Create: `backend/HotelBookingEngine.Tests/Features/RoomTypes/RoomTypesEndpointsTests.cs`

**Interfaces:**
- Consumes: `Program`, `AppDbContext`, `User`/`Role` (Phase 2), `POST /api/auth/login`, `POST /api/hotels`, all `/api/hotels/{hotelId}/room-types` and `/api/room-types/{id}` routes (Task 5).

- [ ] **Step 1: Write the tests**

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HotelBookingEngine.Api.Features.Auth;
using HotelBookingEngine.Api.Features.Hotels;
using HotelBookingEngine.Api.Features.RoomTypes;
using HotelBookingEngine.Api.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace HotelBookingEngine.Tests.Features.RoomTypes;

public class RoomTypesEndpointsTests : IDisposable
{
    private const string ReceptionistUsername = "receptionist-test";
    private const string ReceptionistPassword = "Reception123!";

    private readonly string _dbPath;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public RoomTypesEndpointsTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"hotelbookingengine-roomtypes-tests-{Guid.NewGuid():N}.db");

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

    private async Task<int> CreateHotelAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/hotels", new
        {
            Name = "Grand Hotel",
            Address = "123 Main St",
            City = "Springfield",
            Phone = "555-0100"
        });
        var hotel = await response.Content.ReadFromJsonAsync<HotelResponse>();
        return hotel!.Id;
    }

    private static object SampleRequestBody(string name = "Deluxe") => new
    {
        Name = name,
        Description = "Spacious room with a view",
        Capacity = 2,
        DailyRate = 150m
    };

    [Fact]
    public async Task ListByHotel_WithUnknownHotelId_ReturnsNotFound()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));

        var response = await _client.GetAsync("/api/hotels/999/room-types");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithUnknownHotelId_ReturnsNotFound()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));

        var response = await _client.PostAsJsonAsync("/api/hotels/999/room-types", SampleRequestBody());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListByHotel_AsReceptionist_ReturnsOk()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();

        AuthorizeAs(await LoginAsync(ReceptionistUsername, ReceptionistPassword));
        var response = await _client.GetAsync($"/api/hotels/{hotelId}/room-types");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsReceptionist_ReturnsForbidden()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();

        AuthorizeAs(await LoginAsync(ReceptionistUsername, ReceptionistPassword));
        var response = await _client.PostAsJsonAsync($"/api/hotels/{hotelId}/room-types", SampleRequestBody());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsAdmin_ReturnsCreatedAndThenListIncludesIt()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();

        var createResponse = await _client.PostAsJsonAsync($"/api/hotels/{hotelId}/room-types", SampleRequestBody());
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<RoomTypeResponse>();
        Assert.NotNull(created);
        Assert.Equal("Deluxe", created!.Name);
        Assert.Equal(hotelId, created.HotelId);

        var listResponse = await _client.GetAsync($"/api/hotels/{hotelId}/room-types");
        var roomTypes = await listResponse.Content.ReadFromJsonAsync<List<RoomTypeResponse>>();
        Assert.Contains(roomTypes!, rt => rt.Id == created.Id);
    }

    [Fact]
    public async Task Update_AsReceptionist_ReturnsForbidden()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();
        var created = await (await _client.PostAsJsonAsync($"/api/hotels/{hotelId}/room-types", SampleRequestBody()))
            .Content.ReadFromJsonAsync<RoomTypeResponse>();

        AuthorizeAs(await LoginAsync(ReceptionistUsername, ReceptionistPassword));
        var response = await _client.PutAsJsonAsync($"/api/room-types/{created!.Id}", SampleRequestBody("Updated Name"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_AsAdmin_ReturnsOkWithUpdatedData()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();
        var created = await (await _client.PostAsJsonAsync($"/api/hotels/{hotelId}/room-types", SampleRequestBody()))
            .Content.ReadFromJsonAsync<RoomTypeResponse>();

        var response = await _client.PutAsJsonAsync($"/api/room-types/{created!.Id}", SampleRequestBody("Updated Deluxe"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<RoomTypeResponse>();
        Assert.Equal("Updated Deluxe", updated!.Name);
        Assert.Equal(created.Id, updated.Id);
    }

    [Fact]
    public async Task Delete_AsReceptionist_ReturnsForbidden()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();
        var created = await (await _client.PostAsJsonAsync($"/api/hotels/{hotelId}/room-types", SampleRequestBody()))
            .Content.ReadFromJsonAsync<RoomTypeResponse>();

        AuthorizeAs(await LoginAsync(ReceptionistUsername, ReceptionistPassword));
        var response = await _client.DeleteAsync($"/api/room-types/{created!.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsAdmin_RemovesRoomType()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();
        var created = await (await _client.PostAsJsonAsync($"/api/hotels/{hotelId}/room-types", SampleRequestBody()))
            .Content.ReadFromJsonAsync<RoomTypeResponse>();

        var deleteResponse = await _client.DeleteAsync($"/api/room-types/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/api/room-types/{created.Id}");
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

Save as `backend/HotelBookingEngine.Tests/Features/RoomTypes/RoomTypesEndpointsTests.cs`.

- [ ] **Step 2: Run the tests**

Run: `dotnet test backend/HotelBookingEngine.Tests --filter RoomTypesEndpointsTests`
Expected: PASS (10 passed).

- [ ] **Step 3: Run the full suite**

Run: `dotnet test backend/HotelBookingEngine.sln`
Expected: all tests pass, pristine output.

- [ ] **Step 4: Commit**

```bash
git add backend
git commit -m "test: add integration tests for room type endpoints"
```

---

### Task 7: Room type types and frontend API service

**Files:**
- Create: `frontend/src/types/roomType.ts`
- Create: `frontend/src/features/roomTypes/roomTypeService.ts`

**Interfaces:**
- Produces: `RoomType { id, hotelId, name, description, capacity, dailyRate }`, `RoomTypeRequest { name, description, capacity, dailyRate }`, `listRoomTypes(hotelId)`, `getRoomType(id)`, `createRoomType(hotelId, request)`, `updateRoomType(id, request)`, `deleteRoomType(id)`. Tasks 8-9 consume these.

- [ ] **Step 1: Create the types**

```typescript
export interface RoomType {
  id: number;
  hotelId: number;
  name: string;
  description: string;
  capacity: number;
  dailyRate: number;
}

export interface RoomTypeRequest {
  name: string;
  description: string;
  capacity: number;
  dailyRate: number;
}
```

Save as `frontend/src/types/roomType.ts`.

- [ ] **Step 2: Create the API service**

```typescript
import { httpClient } from "../../api/httpClient";
import type { RoomType, RoomTypeRequest } from "../../types/roomType";

export async function listRoomTypes(hotelId: number): Promise<RoomType[]> {
  const response = await httpClient.get<RoomType[]>(`/api/hotels/${hotelId}/room-types`);
  return response.data;
}

export async function getRoomType(id: number): Promise<RoomType> {
  const response = await httpClient.get<RoomType>(`/api/room-types/${id}`);
  return response.data;
}

export async function createRoomType(hotelId: number, request: RoomTypeRequest): Promise<RoomType> {
  const response = await httpClient.post<RoomType>(`/api/hotels/${hotelId}/room-types`, request);
  return response.data;
}

export async function updateRoomType(id: number, request: RoomTypeRequest): Promise<RoomType> {
  const response = await httpClient.put<RoomType>(`/api/room-types/${id}`, request);
  return response.data;
}

export async function deleteRoomType(id: number): Promise<void> {
  await httpClient.delete(`/api/room-types/${id}`);
}
```

Save as `frontend/src/features/roomTypes/roomTypeService.ts`.

- [ ] **Step 3: Verify**

```bash
cd frontend && npm run build
```

Expected: 0 TypeScript errors.

- [ ] **Step 4: Commit**

```bash
git add frontend
git commit -m "feat: add room type types and API service"
```

---

### Task 8: Hotel Room Types list page, routing, and the Hotels page `409` handler

**Files:**
- Create: `frontend/src/pages/HotelRoomTypesPage.tsx`
- Modify: `frontend/src/routes/AppRoutes.tsx`
- Modify: `frontend/src/pages/HotelsPage.tsx`

**Interfaces:**
- Consumes: `listRoomTypes`, `deleteRoomType` (Task 7), `getHotel` (Phase 3), `ProtectedRoute` (Phase 3), `useAuth()` (Phase 2).
- Produces: route `/hotels/:hotelId/room-types`.

- [ ] **Step 1: Create the Hotel Room Types list page**

```typescript
import { Link, useParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { deleteRoomType, listRoomTypes } from "../features/roomTypes/roomTypeService";
import { getHotel } from "../features/hotels/hotelService";
import { useAuth } from "../hooks/useAuth";

export function HotelRoomTypesPage() {
  const { hotelId: hotelIdParam } = useParams<{ hotelId: string }>();
  const hotelId = Number(hotelIdParam);
  const { user } = useAuth();
  const queryClient = useQueryClient();

  const { data: hotel } = useQuery({
    queryKey: ["hotels", hotelId],
    queryFn: () => getHotel(hotelId),
  });

  const { data, isLoading, isError } = useQuery({
    queryKey: ["hotels", hotelId, "room-types"],
    queryFn: () => listRoomTypes(hotelId),
  });

  const deleteMutation = useMutation({
    mutationFn: deleteRoomType,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["hotels", hotelId, "room-types"] });
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
      <Link to="/hotels" className="text-blue-600 underline">
        Back to Hotels
      </Link>
      <h1 className="text-2xl font-semibold">Room Types{hotel ? ` — ${hotel.name}` : ""}</h1>

      {isAdmin && (
        <Link to={`/hotels/${hotelId}/room-types/new`} className="text-blue-600 underline">
          New Room Type
        </Link>
      )}

      {isLoading && <p>Loading room types...</p>}
      {isError && <p>Unable to load room types.</p>}

      {data && (
        <table className="w-full max-w-3xl border-collapse">
          <thead>
            <tr className="border-b text-left">
              <th className="p-2">Name</th>
              <th className="p-2">Description</th>
              <th className="p-2">Capacity</th>
              <th className="p-2">Daily Rate</th>
              {isAdmin && <th className="p-2">Actions</th>}
            </tr>
          </thead>
          <tbody>
            {data.map((roomType) => (
              <tr key={roomType.id} className="border-b">
                <td className="p-2">{roomType.name}</td>
                <td className="p-2">{roomType.description}</td>
                <td className="p-2">{roomType.capacity}</td>
                <td className="p-2">{roomType.dailyRate}</td>
                {isAdmin && (
                  <td className="flex gap-2 p-2">
                    <Link to={`/hotels/${hotelId}/room-types/${roomType.id}/edit`} className="text-blue-600 underline">
                      Edit
                    </Link>
                    <button
                      onClick={() => handleDelete(roomType.id, roomType.name)}
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

Save as `frontend/src/pages/HotelRoomTypesPage.tsx`.

- [ ] **Step 2: Add the `/hotels/:hotelId/room-types` route**

Replace the contents of `frontend/src/routes/AppRoutes.tsx` with:

```typescript
import { Routes, Route } from "react-router-dom";
import { HomePage } from "../pages/HomePage";
import { LoginPage } from "../pages/LoginPage";
import { HotelsPage } from "../pages/HotelsPage";
import { HotelFormPage } from "../pages/HotelFormPage";
import { HotelRoomTypesPage } from "../pages/HotelRoomTypesPage";
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
      <Route
        path="/hotels/:hotelId/room-types"
        element={
          <ProtectedRoute>
            <HotelRoomTypesPage />
          </ProtectedRoute>
        }
      />
    </Routes>
  );
}
```

- [ ] **Step 3: Add a "Room Types" link per row and the `onError` handler to `HotelsPage`**

Replace the contents of `frontend/src/pages/HotelsPage.tsx` with:

```typescript
import { useState } from "react";
import { Link } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { AxiosError } from "axios";
import { deleteHotel, listHotels } from "../features/hotels/hotelService";
import { useAuth } from "../hooks/useAuth";

export function HotelsPage() {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const { data, isLoading, isError } = useQuery({
    queryKey: ["hotels"],
    queryFn: listHotels,
  });

  const deleteMutation = useMutation({
    mutationFn: deleteHotel,
    onSuccess: () => {
      setDeleteError(null);
      queryClient.invalidateQueries({ queryKey: ["hotels"] });
    },
    onError: (error: AxiosError) => {
      const message =
        typeof error.response?.data === "string" ? error.response.data : "Unable to delete this hotel.";
      setDeleteError(message);
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
      {deleteError && <p className="text-sm text-red-600">{deleteError}</p>}

      {data && (
        <table className="w-full max-w-3xl border-collapse">
          <thead>
            <tr className="border-b text-left">
              <th className="p-2">Name</th>
              <th className="p-2">Address</th>
              <th className="p-2">City</th>
              <th className="p-2">Phone</th>
              <th className="p-2">Room Types</th>
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
                <td className="p-2">
                  <Link to={`/hotels/${hotel.id}/room-types`} className="text-blue-600 underline">
                    Room Types
                  </Link>
                </td>
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

- [ ] **Step 4: Verify**

```bash
cd frontend && npm run build
```

Expected: 0 TypeScript errors.

- [ ] **Step 5: Commit**

```bash
git add frontend
git commit -m "feat: add hotel room types list page and hotel delete conflict message"
```

---

### Task 9: Room Type create/edit form, remaining routes, manual verification, README

**Files:**
- Create: `frontend/src/pages/RoomTypeFormPage.tsx`
- Modify: `frontend/src/routes/AppRoutes.tsx`
- Modify: `README.md`

**Interfaces:**
- Consumes: `createRoomType`, `updateRoomType`, `getRoomType` (Task 7), `ProtectedRoute` (Phase 3).
- Produces: routes `/hotels/:hotelId/room-types/new`, `/hotels/:hotelId/room-types/:id/edit`.

- [ ] **Step 1: Create the Room Type form page**

```typescript
import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useNavigate, useParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createRoomType, getRoomType, updateRoomType } from "../features/roomTypes/roomTypeService";

const roomTypeSchema = z.object({
  name: z.string().min(1, "Name is required").max(100),
  description: z.string().min(1, "Description is required").max(500),
  capacity: z.coerce.number().int().positive("Capacity must be greater than 0"),
  dailyRate: z.coerce.number().positive("Daily rate must be greater than 0"),
});

type RoomTypeFormValues = z.infer<typeof roomTypeSchema>;

export function RoomTypeFormPage() {
  const { hotelId: hotelIdParam, id } = useParams<{ hotelId: string; id: string }>();
  const hotelId = Number(hotelIdParam);
  const roomTypeId = id ? Number(id) : undefined;
  const isEditMode = roomTypeId !== undefined;
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const { data: existingRoomType } = useQuery({
    queryKey: ["room-types", roomTypeId],
    queryFn: () => getRoomType(roomTypeId!),
    enabled: isEditMode,
  });

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<RoomTypeFormValues>({
    resolver: zodResolver(roomTypeSchema),
  });

  useEffect(() => {
    if (existingRoomType) {
      reset({
        name: existingRoomType.name,
        description: existingRoomType.description,
        capacity: existingRoomType.capacity,
        dailyRate: existingRoomType.dailyRate,
      });
    }
  }, [existingRoomType, reset]);

  const mutation = useMutation({
    mutationFn: (values: RoomTypeFormValues) =>
      isEditMode ? updateRoomType(roomTypeId!, values) : createRoomType(hotelId, values),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["hotels", hotelId, "room-types"] });
      navigate(`/hotels/${hotelId}/room-types`);
    },
  });

  function onSubmit(values: RoomTypeFormValues) {
    mutation.mutate(values);
  }

  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-4">
      <h1 className="text-2xl font-semibold">{isEditMode ? "Edit Room Type" : "New Room Type"}</h1>
      <form onSubmit={handleSubmit(onSubmit)} className="flex w-full max-w-sm flex-col gap-3">
        <div>
          <label htmlFor="name" className="block text-sm font-medium">
            Name
          </label>
          <input id="name" type="text" className="w-full rounded border px-3 py-2" {...register("name")} />
          {errors.name && <p className="text-sm text-red-600">{errors.name.message}</p>}
        </div>
        <div>
          <label htmlFor="description" className="block text-sm font-medium">
            Description
          </label>
          <input
            id="description"
            type="text"
            className="w-full rounded border px-3 py-2"
            {...register("description")}
          />
          {errors.description && <p className="text-sm text-red-600">{errors.description.message}</p>}
        </div>
        <div>
          <label htmlFor="capacity" className="block text-sm font-medium">
            Capacity
          </label>
          <input id="capacity" type="number" className="w-full rounded border px-3 py-2" {...register("capacity")} />
          {errors.capacity && <p className="text-sm text-red-600">{errors.capacity.message}</p>}
        </div>
        <div>
          <label htmlFor="dailyRate" className="block text-sm font-medium">
            Daily Rate
          </label>
          <input
            id="dailyRate"
            type="number"
            step="0.01"
            className="w-full rounded border px-3 py-2"
            {...register("dailyRate")}
          />
          {errors.dailyRate && <p className="text-sm text-red-600">{errors.dailyRate.message}</p>}
        </div>
        <button
          type="submit"
          disabled={mutation.isPending}
          className="rounded bg-blue-600 px-4 py-2 text-white disabled:opacity-50"
        >
          {mutation.isPending ? "Saving..." : "Save"}
        </button>
      </form>
    </main>
  );
}
```

Save as `frontend/src/pages/RoomTypeFormPage.tsx`.

- [ ] **Step 2: Add the remaining routes**

Replace the contents of `frontend/src/routes/AppRoutes.tsx` with:

```typescript
import { Routes, Route } from "react-router-dom";
import { HomePage } from "../pages/HomePage";
import { LoginPage } from "../pages/LoginPage";
import { HotelsPage } from "../pages/HotelsPage";
import { HotelFormPage } from "../pages/HotelFormPage";
import { HotelRoomTypesPage } from "../pages/HotelRoomTypesPage";
import { RoomTypeFormPage } from "../pages/RoomTypeFormPage";
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
      <Route
        path="/hotels/:hotelId/room-types"
        element={
          <ProtectedRoute>
            <HotelRoomTypesPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/hotels/:hotelId/room-types/new"
        element={
          <ProtectedRoute roles={["Admin"]}>
            <RoomTypeFormPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/hotels/:hotelId/room-types/:id/edit"
        element={
          <ProtectedRoute roles={["Admin"]}>
            <RoomTypeFormPage />
          </ProtectedRoute>
        }
      />
    </Routes>
  );
}
```

- [ ] **Step 3: Verify the build**

```bash
cd frontend && npm run build
```

Expected: 0 TypeScript errors.

- [ ] **Step 4: Manual end-to-end verification**

Terminal 1: `dotnet run --project backend/HotelBookingEngine.Api`
Terminal 2: `cd frontend && npm run dev`

In a browser:
1. Log in as `admin`/`Admin123!`. Go to `/hotels`, click "Room Types" on a hotel row → `/hotels/:hotelId/room-types` loads, empty list, "New Room Type" link visible (Admin).
2. Click "New Room Type" → fill the form → Save → redirected to the room types list, the new room type appears in the table.
3. Click "Edit" on that row → form pre-filled with current values → change the name → Save → redirected back, table shows the updated name.
4. Click "Delete" → confirm dialog → row disappears from the table.
5. Go back to `/hotels`, try to "Delete" the hotel that still has this room type → the delete fails and the page shows an error message (the new `409` handling). Delete the room type first, then delete the hotel — it now succeeds.
6. Navigate directly to `/hotels/1/room-types/new` while logged out → redirected to `/login`.
7. (Optional, requires a second account) Confirm a Receptionist-role user sees the room types table without "New Room Type"/"Edit"/"Delete" controls, and is redirected away from the form routes if navigated to directly. This exact enforcement is already proven by Task 6's integration tests at the API level.

Stop both processes once confirmed. This step needs a human/browser to actually execute — describe the result when reporting back rather than assuming it passed.

- [ ] **Step 5: Update the README**

Update the "Status" line in `README.md` to: `Phase 1, Phase 2 (Authentication), Phase 3 (Hotels), and Phase 4 (Room Types) complete. Next: Phase 5 — Rooms.`

- [ ] **Step 6: Commit**

```bash
git add frontend README.md
git commit -m "feat: add room type create/edit form and wire remaining routes"
```

---

## Self-Review Notes

- **Spec coverage:** Create/Update/Delete/List RoomType (Tasks 1, 4-9), `RoomType` scoped to a `Hotel` via required `HotelId` (Task 1), `Capacity`/`DailyRate` on `RoomType` not `Room` (Task 1's field list, called out in Global Constraints), Restrict-not-cascade on hotel delete with the `HotelDeleteResult` contract change (Tasks 2-3), the two-URL-shape routing on one controller (Task 5), `null`-means-hotel-not-found on `CreateAsync`/`ListByHotelAsync` (Task 4), authorization mirroring Hotels (Tasks 5-6), frontend nested routes and `HotelsPage` "Room Types" link (Task 8), the `HotelsPage` delete `onError` handler closing the Phase 3 review gap (Task 8), and the explicit non-treatment of `HotelRoomTypesPage`'s delete errors (Global Constraints, deferred to Phase 5) are all covered. Rooms/Guests/Reservations/Dashboard and cross-hotel room type views are untouched, as required.
- **Placeholder scan:** no TODO/TBD; all code blocks are complete and consistent with the established Phase 1-3 patterns (SQLite-backed unit tests, `WebApplicationFactory` integration tests, manual DTO mapping, thin controllers, `mutation.isPending` for submit-button disabling).
- **Type consistency:** `RoomTypeResponse` (C#: `Id, HotelId, Name, Description, Capacity, DailyRate`) matches TS `RoomType` (`id, hotelId, name, description, capacity, dailyRate`) via ASP.NET Core's default camelCase policy. `RoomTypeRequest`/`IRoomTypeService` signatures are used identically across Tasks 4-6. `HotelDeleteResult` values (`Deleted`/`NotFound`/`HasRoomTypes`) are used identically in `HotelService` (Task 2), `HotelsController` (Task 3), and their respective tests. `listRoomTypes(hotelId)`/`getRoomType(id)`/`createRoomType(hotelId, request)`/`updateRoomType(id, request)`/`deleteRoomType(id)` signatures from Task 7 are called identically in Tasks 8-9.
