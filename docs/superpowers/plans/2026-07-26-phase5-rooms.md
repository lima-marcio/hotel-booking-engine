# Phase 5 - Rooms Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add full Rooms CRUD (Create, Update, Delete, List) to the Hotel Booking Engine, backend and frontend. `Room` is the third feature slice and the second parent/child relationship — it belongs to a `RoomType` and, denormalized, directly to a `Hotel` too.

**Architecture:** A `Features/Rooms/` vertical slice mirrors `Features/RoomTypes/`: entity + Fluent API configuration, a shared request DTO + response DTO, a service, and a thin controller. `Room.HotelId` is copied from its `RoomType.HotelId` at creation and never independently settable. Because Create/Update now have two distinct failure modes (parent not found vs. a duplicate room number within the hotel), `IRoomService.CreateAsync`/`UpdateAsync` return a small `RoomSaveResult` (outcome + payload) instead of a bare nullable. Routing splits across two URL shapes on one controller, one level deeper than Phase 4: room-type-scoped list/create (`/api/room-types/{roomTypeId}/rooms`) and flat get/update/delete by id (`/api/rooms/{id}`). `IRoomTypeService.DeleteAsync`'s contract changes from `Task<bool>` to `Task<RoomTypeDeleteResult>`, mirroring Phase 4's `HotelDeleteResult`, so `RoomTypesController` can return `409` instead of silently cascading when a room type still has rooms. The frontend adds a room-type-scoped list page and a shared create/edit form page, both gated by the existing `ProtectedRoute`, plus an `onError` handler on `HotelRoomTypesPage`'s delete mutation (closing the gap Phase 4 deliberately deferred) and a new `onError` handler on the room form's save mutation (the project's first form-level, not just delete-level, business-rule error).

**Tech Stack:** No new backend packages (EF Core, `Microsoft.AspNetCore.Authorization`, `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.Data.Sqlite` already present). No new frontend packages (React Router, TanStack Query, React Hook Form, Zod, `@hookform/resolvers`, Axios already present).

**Design spec:** `docs/superpowers/specs/2026-07-26-phase5-rooms-design.md` — read for full rationale; this plan implements it as-is.

## Global Constraints

- `Room` fields: `RoomTypeId` (required FK → `RoomType.Id`, `OnDelete(Restrict)`), `HotelId` (required FK → `Hotel.Id`, `OnDelete(Restrict)`, copied from `RoomType.HotelId` at creation, never client-supplied), `RoomNumber` (required, max length 20), `Status` (required `RoomStatus` enum: `Available`, `Maintenance`).
- `RoomNumber` must be unique within a `Hotel` (not within a `RoomType`), enforced in `RoomService` at the application layer — no DB-level unique index, consistent with how this project enforces business rules in services rather than at the schema level beyond required/max-length.
- `RoomStatus` is bound as a typed enum directly on `RoomRequest`/`RoomResponse` via `[JsonConverter(typeof(JsonStringEnumConverter))]` (on the property for `RoomResponse`, a record). An invalid status string in a request body fails with an automatic `400` from ASP.NET Core's model binding before any service code runs.
- Deleting a `RoomType` that still has `Room`s is blocked (Restrict), not cascaded — the same pattern Phase 4 established for `Hotel` → `RoomType`. `IRoomTypeService.DeleteAsync` changes from `Task<bool>` to `Task<RoomTypeDeleteResult>` where `RoomTypeDeleteResult` is `{ Deleted, NotFound, HasRooms }`. `RoomTypesController.Delete` maps `Deleted → 204`, `NotFound → 404`, `HasRooms → 409`.
- `Room` → `Hotel` also gets `DeleteBehavior.Restrict` at the EF level, as defense-in-depth — no new check is added to `HotelService`, because a `Hotel` can never have a `Room` without first having the `RoomType` that `Room` requires, and `RoomType` deletion is already blocked while `Room`s reference it.
- `IRoomService.CreateAsync(roomTypeId, request)`/`UpdateAsync(id, request)` return `RoomSaveResult(RoomSaveOutcome Outcome, RoomResponse? Room)` where `RoomSaveOutcome` is `{ Success, ParentNotFound, DuplicateRoomNumber }`. `RoomsController` maps `Success → 201`/`200`, `ParentNotFound → 404`, `DuplicateRoomNumber → 409`.
- Routing: `GET`/`POST /api/room-types/{roomTypeId}/rooms` (list/create, room-type-scoped); `GET`/`PUT`/`DELETE /api/rooms/{id}` (flat, id is already globally unique). One controller (`RoomsController`), no class-level `[Route]` — each action has its own absolute route template.
- `RoomService.ListByRoomTypeAsync` returns `null` specifically to mean "the room type doesn't exist" (→ `404`), distinct from an empty list.
- Authorization mirrors Hotels/RoomTypes exactly: `GET` requires only `[Authorize]`; `POST`/`PUT`/`DELETE` require `[Authorize(Roles="Admin")]` in addition.
- Single shared `RoomRequest` DTO for both Create and Update (no `RoomTypeId`/`HotelId` on it). Manual mapping in `RoomService`, no AutoMapper.
- Frontend routes nest under the room type: `/hotels/:hotelId/room-types/:roomTypeId/rooms`, `/hotels/:hotelId/room-types/:roomTypeId/rooms/new`, `/hotels/:hotelId/room-types/:roomTypeId/rooms/:id/edit`. `hotelId` stays in the URL purely for the back-link; the API never needs it. `HotelRoomTypesPage` gains a "Rooms" link per row, visible to any authenticated user.
- `HotelRoomTypesPage`'s delete mutation gains an `onError` handler identical in shape to `HotelsPage`'s (Phase 4), surfacing the backend's new `409` — closing the gap Phase 4's design doc explicitly deferred to this phase.
- `RoomFormPage`'s create/update mutation gains an `onError` handler surfacing the backend's `409` (duplicate room number), using the same `isAxiosError` type guard fixed on Phase 4's delete-error handling. `RoomFormPage` also redirects if the loaded room's `roomTypeId` doesn't match the URL's `roomTypeId`, mirroring the fix already applied to `RoomTypeFormPage`.
- Frontend Zod validation mirrors the backend: `roomNumber` required, max 20; `status` one of `"Available"`/`"Maintenance"`.
- Delete confirmation uses `window.confirm` — no custom modal, matching prior phases.
- Submit buttons disable via `mutation.isPending` while a create/update request is in flight.
- Controllers contain no business rules; services contain business rules. [10-backend.md]
- Dependency Injection through extension methods. [10-backend.md]
- Fluent API with `IEntityTypeConfiguration`. [10-backend.md]
- One class per file, clear names, no abbreviations, no TODOs, no commented dead code, async/await for I/O-bound work. [30-conventions.md]
- Commit messages follow Conventional Commits, no `Co-Authored-By`/session trailers. [30-conventions.md]
- Never hardcode data in the frontend; consume only the backend API. [prompts/project-01.md]
- Do not implement Guests, Reservations, or Dashboard — those are later phases. Do not build any UI for re-parenting a room to a different room type/hotel, or for viewing a hotel's rooms without going through a room type.

---

### Task 1: `Room` entity, `RoomStatus` enum, Fluent API configuration, and migration

**Files:**
- Create: `backend/HotelBookingEngine.Api/Features/Rooms/RoomStatus.cs`
- Create: `backend/HotelBookingEngine.Api/Features/Rooms/Room.cs`
- Create: `backend/HotelBookingEngine.Api/Features/Rooms/RoomConfiguration.cs`
- Modify: `backend/HotelBookingEngine.Api/Persistence/AppDbContext.cs`
- Create: `backend/HotelBookingEngine.Api/Persistence/Migrations/*_AddRooms.cs` (generated)

**Interfaces:**
- Consumes: `RoomType` (Phase 4, `Features/RoomTypes/RoomType.cs`), `Hotel` (Phase 3, `Features/Hotels/Hotel.cs`) — the FK targets.
- Produces: `Room { Id, RoomTypeId, HotelId, RoomNumber, Status }`, `RoomStatus { Available, Maintenance }`, `AppDbContext.Rooms : DbSet<Room>`. Task 2's `RoomTypeService` queries this DbSet for the Restrict-on-delete check; Task 4's `RoomService` constructs/queries `Room` via this DbSet.

- [ ] **Step 1: Create the `RoomStatus` enum**

```csharp
namespace HotelBookingEngine.Api.Features.Rooms;

public enum RoomStatus
{
    Available,
    Maintenance
}
```

Save as `backend/HotelBookingEngine.Api/Features/Rooms/RoomStatus.cs`.

- [ ] **Step 2: Create the `Room` entity**

```csharp
namespace HotelBookingEngine.Api.Features.Rooms;

public class Room
{
    public int Id { get; set; }
    public int RoomTypeId { get; set; }
    public int HotelId { get; set; }
    public required string RoomNumber { get; set; }
    public RoomStatus Status { get; set; }
}
```

Save as `backend/HotelBookingEngine.Api/Features/Rooms/Room.cs`.

- [ ] **Step 3: Create the Fluent API configuration**

```csharp
using HotelBookingEngine.Api.Features.Hotels;
using HotelBookingEngine.Api.Features.RoomTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelBookingEngine.Api.Features.Rooms;

public class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.RoomNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(r => r.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.HasOne<RoomType>()
            .WithMany()
            .HasForeignKey(r => r.RoomTypeId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Hotel>()
            .WithMany()
            .HasForeignKey(r => r.HotelId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

Save as `backend/HotelBookingEngine.Api/Features/Rooms/RoomConfiguration.cs`. Both foreign keys get `DeleteBehavior.Restrict` explicitly — `Microsoft.Data.Sqlite` enforces FK constraints by default, so without this, EF Core's default behavior for a required relationship could otherwise allow an unexpected cascade if a future code path ever bypassed the service-layer guards.

- [ ] **Step 4: Wire `Room` into `AppDbContext`**

Replace the contents of `backend/HotelBookingEngine.Api/Persistence/AppDbContext.cs` with:

```csharp
using Microsoft.EntityFrameworkCore;
using HotelBookingEngine.Api.Features.Auth;
using HotelBookingEngine.Api.Features.Hotels;
using HotelBookingEngine.Api.Features.RoomTypes;
using HotelBookingEngine.Api.Features.Rooms;

namespace HotelBookingEngine.Api.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Hotel> Hotels => Set<Hotel>();
    public DbSet<RoomType> RoomTypes => Set<RoomType>();
    public DbSet<Room> Rooms => Set<Room>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

- [ ] **Step 5: Generate the migration**

```bash
dotnet ef migrations add AddRooms --project backend/HotelBookingEngine.Api --output-dir Persistence/Migrations
```

- [ ] **Step 6: Verify**

```bash
dotnet build backend/HotelBookingEngine.sln
```

Expected: build succeeds. Open the generated migration and confirm it creates a `Rooms` table with two foreign keys (`RoomTypeId` → `RoomTypes`, `HotelId` → `Hotels`), both with restrict/no-action delete behavior, and no seed data.

- [ ] **Step 7: Commit**

```bash
git add backend
git commit -m "feat: add Room entity and migration"
```

---

### Task 2: `RoomTypeService.DeleteAsync` → `RoomTypeDeleteResult` contract change (TDD)

**Files:**
- Create: `backend/HotelBookingEngine.Api/Features/RoomTypes/RoomTypeDeleteResult.cs`
- Modify: `backend/HotelBookingEngine.Api/Features/RoomTypes/IRoomTypeService.cs`
- Modify: `backend/HotelBookingEngine.Api/Features/RoomTypes/RoomTypeService.cs`
- Modify: `backend/HotelBookingEngine.Tests/Features/RoomTypes/RoomTypeServiceTests.cs`

**Interfaces:**
- Consumes: `AppDbContext.Rooms` (Task 1).
- Produces: `RoomTypeDeleteResult { Deleted, NotFound, HasRooms }`, `IRoomTypeService.DeleteAsync(int, CancellationToken) : Task<RoomTypeDeleteResult>` (was `Task<bool>`). Task 3's `RoomTypesController.Delete` maps these three outcomes to HTTP status codes.

- [ ] **Step 1: Update the failing/changed tests**

Add `using HotelBookingEngine.Api.Features.Rooms;` to the top of `backend/HotelBookingEngine.Tests/Features/RoomTypes/RoomTypeServiceTests.cs` (alongside the existing usings), then replace:

```csharp
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
```

with:

```csharp
    [Fact]
    public async Task DeleteAsync_WithExistingId_RemovesRoomTypeAndReturnsDeleted()
    {
        var hotelId = await CreateHotelAsync();
        var created = await _sut.CreateAsync(hotelId, SampleRequest(), CancellationToken.None);

        var result = await _sut.DeleteAsync(created!.Id, CancellationToken.None);

        Assert.Equal(RoomTypeDeleteResult.Deleted, result);
        Assert.Null(await _dbContext.RoomTypes.FindAsync(created.Id));
    }

    [Fact]
    public async Task DeleteAsync_WithUnknownId_ReturnsNotFound()
    {
        var result = await _sut.DeleteAsync(999, CancellationToken.None);

        Assert.Equal(RoomTypeDeleteResult.NotFound, result);
    }

    [Fact]
    public async Task DeleteAsync_WithExistingRooms_ReturnsHasRoomsAndDoesNotDelete()
    {
        var hotelId = await CreateHotelAsync();
        var created = await _sut.CreateAsync(hotelId, SampleRequest(), CancellationToken.None);
        _dbContext.Rooms.Add(new Room
        {
            RoomTypeId = created!.Id,
            HotelId = hotelId,
            RoomNumber = "101",
            Status = RoomStatus.Available
        });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.DeleteAsync(created.Id, CancellationToken.None);

        Assert.Equal(RoomTypeDeleteResult.HasRooms, result);
        Assert.NotNull(await _dbContext.RoomTypes.FindAsync(created.Id));
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/HotelBookingEngine.Tests --filter RoomTypeServiceTests`
Expected: FAIL (build error — `RoomTypeDeleteResult` doesn't exist yet, and `DeleteAsync` still returns `bool`).

- [ ] **Step 3: Add the `RoomTypeDeleteResult` enum**

```csharp
namespace HotelBookingEngine.Api.Features.RoomTypes;

public enum RoomTypeDeleteResult
{
    Deleted,
    NotFound,
    HasRooms
}
```

Save as `backend/HotelBookingEngine.Api/Features/RoomTypes/RoomTypeDeleteResult.cs`.

- [ ] **Step 4: Update `IRoomTypeService`**

```csharp
namespace HotelBookingEngine.Api.Features.RoomTypes;

public interface IRoomTypeService
{
    Task<RoomTypeResponse?> CreateAsync(int hotelId, RoomTypeRequest request, CancellationToken cancellationToken);
    Task<RoomTypeResponse?> UpdateAsync(int id, RoomTypeRequest request, CancellationToken cancellationToken);
    Task<RoomTypeDeleteResult> DeleteAsync(int id, CancellationToken cancellationToken);
    Task<RoomTypeResponse?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<List<RoomTypeResponse>?> ListByHotelAsync(int hotelId, CancellationToken cancellationToken);
}
```

Save as `backend/HotelBookingEngine.Api/Features/RoomTypes/IRoomTypeService.cs`.

- [ ] **Step 5: Update `RoomTypeService.DeleteAsync`**

In `backend/HotelBookingEngine.Api/Features/RoomTypes/RoomTypeService.cs`, replace:

```csharp
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
```

with:

```csharp
    public async Task<RoomTypeDeleteResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var roomType = await _dbContext.RoomTypes.FindAsync([id], cancellationToken);
        if (roomType is null)
        {
            return RoomTypeDeleteResult.NotFound;
        }

        var hasRooms = await _dbContext.Rooms.AnyAsync(r => r.RoomTypeId == id, cancellationToken);
        if (hasRooms)
        {
            return RoomTypeDeleteResult.HasRooms;
        }

        _dbContext.RoomTypes.Remove(roomType);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return RoomTypeDeleteResult.Deleted;
    }
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test backend/HotelBookingEngine.Tests --filter RoomTypeServiceTests`
Expected: PASS (11 passed — the 10 original tests, with the 2 delete tests updated to the new contract, plus the new `HasRooms` test).

- [ ] **Step 7: Commit**

```bash
git add backend
git commit -m "feat: change RoomTypeService.DeleteAsync to return RoomTypeDeleteResult"
```

---

### Task 3: `RoomTypesController.Delete` mapping and the `409` integration test

**Files:**
- Modify: `backend/HotelBookingEngine.Api/Features/RoomTypes/RoomTypesController.cs`
- Modify: `backend/HotelBookingEngine.Tests/Features/RoomTypes/RoomTypesEndpointsTests.cs`

**Interfaces:**
- Consumes: `RoomTypeDeleteResult` (Task 2).
- Produces: `DELETE /api/room-types/{id}` now returns `204`/`404`/`409`. Task 8's frontend `onError` handler on the room type delete mutation depends on the `409` existing.

- [ ] **Step 1: Update `RoomTypesController.Delete`**

In `backend/HotelBookingEngine.Api/Features/RoomTypes/RoomTypesController.cs`, replace:

```csharp
    [HttpDelete("api/room-types/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _roomTypeService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
```

with:

```csharp
    [HttpDelete("api/room-types/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _roomTypeService.DeleteAsync(id, cancellationToken);
        return result switch
        {
            RoomTypeDeleteResult.Deleted => NoContent(),
            RoomTypeDeleteResult.NotFound => NotFound(),
            RoomTypeDeleteResult.HasRooms => Conflict("Cannot delete a room type that still has rooms. Delete its rooms first."),
            _ => throw new InvalidOperationException($"Unhandled {nameof(RoomTypeDeleteResult)} value: {result}")
        };
    }
```

- [ ] **Step 2: Add the `409` integration test**

Add `using HotelBookingEngine.Api.Features.Rooms;` to the top of `backend/HotelBookingEngine.Tests/Features/RoomTypes/RoomTypesEndpointsTests.cs`, then add this test (e.g. after `Delete_AsAdmin_RemovesRoomType`):

```csharp
    [Fact]
    public async Task Delete_WithExistingRoom_ReturnsConflict()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();
        var created = await (await _client.PostAsJsonAsync($"/api/hotels/{hotelId}/room-types", SampleRequestBody()))
            .Content.ReadFromJsonAsync<RoomTypeResponse>();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Rooms.Add(new Room
            {
                RoomTypeId = created!.Id,
                HotelId = hotelId,
                RoomNumber = "101",
                Status = RoomStatus.Available
            });
            dbContext.SaveChanges();
        }

        var response = await _client.DeleteAsync($"/api/room-types/{created!.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var getResponse = await _client.GetAsync($"/api/room-types/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }
```

- [ ] **Step 3: Run the tests**

Run: `dotnet test backend/HotelBookingEngine.Tests --filter RoomTypesEndpointsTests`
Expected: PASS (12 passed).

- [ ] **Step 4: Run the full suite**

Run: `dotnet test backend/HotelBookingEngine.sln`
Expected: all tests pass, pristine output.

- [ ] **Step 5: Commit**

```bash
git add backend
git commit -m "feat: return 409 when deleting a room type that still has rooms"
```

---

### Task 4: `RoomService` — CRUD, `HotelId` derivation, and hotel-scoped room-number uniqueness (TDD)

**Files:**
- Create: `backend/HotelBookingEngine.Api/Features/Rooms/RoomRequest.cs`
- Create: `backend/HotelBookingEngine.Api/Features/Rooms/RoomResponse.cs`
- Create: `backend/HotelBookingEngine.Api/Features/Rooms/RoomSaveResult.cs`
- Create: `backend/HotelBookingEngine.Api/Features/Rooms/IRoomService.cs`
- Create: `backend/HotelBookingEngine.Api/Features/Rooms/RoomService.cs`
- Create: `backend/HotelBookingEngine.Tests/Features/Rooms/RoomServiceTests.cs`

**Interfaces:**
- Consumes: `Room`/`RoomStatus`/`AppDbContext` (Task 1), `RoomType` (Phase 4).
- Produces: `IRoomService.CreateAsync(int, RoomRequest, CancellationToken) : Task<RoomSaveResult>`, `UpdateAsync(int, RoomRequest, CancellationToken) : Task<RoomSaveResult>`, `DeleteAsync(int, CancellationToken) : Task<bool>`, `GetByIdAsync(int, CancellationToken) : Task<RoomResponse?>`, `ListByRoomTypeAsync(int, CancellationToken) : Task<List<RoomResponse>?>`. Task 5's `RoomsController` calls these by these exact signatures.

- [ ] **Step 1: Write the failing tests**

```csharp
using HotelBookingEngine.Api.Features.Hotels;
using HotelBookingEngine.Api.Features.RoomTypes;
using HotelBookingEngine.Api.Features.Rooms;
using HotelBookingEngine.Api.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelBookingEngine.Tests.Features.Rooms;

public class RoomServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _dbContext;
    private readonly RoomService _sut;

    public RoomServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();

        _sut = new RoomService(_dbContext);
    }

    private async Task<int> CreateHotelAsync(string name = "Grand Hotel")
    {
        var hotel = new Hotel { Name = name, Address = "123 Main St", City = "Springfield", Phone = "555-0100" };
        _dbContext.Hotels.Add(hotel);
        await _dbContext.SaveChangesAsync();
        return hotel.Id;
    }

    private async Task<int> CreateRoomTypeAsync(int hotelId, string name = "Deluxe")
    {
        var roomType = new RoomType
        {
            HotelId = hotelId,
            Name = name,
            Description = "Spacious room with a view",
            Capacity = 2,
            DailyRate = 150m
        };
        _dbContext.RoomTypes.Add(roomType);
        await _dbContext.SaveChangesAsync();
        return roomType.Id;
    }

    private static RoomRequest SampleRequest(string roomNumber = "101") => new()
    {
        RoomNumber = roomNumber,
        Status = RoomStatus.Available
    };

    [Fact]
    public async Task CreateAsync_WithExistingRoomType_PersistsAndReturnsRoomWithHotelIdFromRoomType()
    {
        var hotelId = await CreateHotelAsync();
        var roomTypeId = await CreateRoomTypeAsync(hotelId);

        var result = await _sut.CreateAsync(roomTypeId, SampleRequest(), CancellationToken.None);

        Assert.Equal(RoomSaveOutcome.Success, result.Outcome);
        Assert.NotNull(result.Room);
        Assert.True(result.Room!.Id > 0);
        Assert.Equal(roomTypeId, result.Room.RoomTypeId);
        Assert.Equal(hotelId, result.Room.HotelId);
        Assert.Equal("101", result.Room.RoomNumber);

        var stored = await _dbContext.Rooms.FindAsync(result.Room.Id);
        Assert.NotNull(stored);
    }

    [Fact]
    public async Task CreateAsync_WithUnknownRoomTypeId_ReturnsParentNotFound()
    {
        var result = await _sut.CreateAsync(999, SampleRequest(), CancellationToken.None);

        Assert.Equal(RoomSaveOutcome.ParentNotFound, result.Outcome);
        Assert.Null(result.Room);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateRoomNumberInSameHotel_ReturnsDuplicateRoomNumber()
    {
        var hotelId = await CreateHotelAsync();
        var roomTypeAId = await CreateRoomTypeAsync(hotelId, "Standard");
        var roomTypeBId = await CreateRoomTypeAsync(hotelId, "Deluxe");
        await _sut.CreateAsync(roomTypeAId, SampleRequest("101"), CancellationToken.None);

        var result = await _sut.CreateAsync(roomTypeBId, SampleRequest("101"), CancellationToken.None);

        Assert.Equal(RoomSaveOutcome.DuplicateRoomNumber, result.Outcome);
        Assert.Null(result.Room);
    }

    [Fact]
    public async Task CreateAsync_WithSameRoomNumberInDifferentHotel_Succeeds()
    {
        var hotelAId = await CreateHotelAsync("Hotel A");
        var hotelBId = await CreateHotelAsync("Hotel B");
        var roomTypeAId = await CreateRoomTypeAsync(hotelAId);
        var roomTypeBId = await CreateRoomTypeAsync(hotelBId);
        await _sut.CreateAsync(roomTypeAId, SampleRequest("101"), CancellationToken.None);

        var result = await _sut.CreateAsync(roomTypeBId, SampleRequest("101"), CancellationToken.None);

        Assert.Equal(RoomSaveOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task UpdateAsync_WithExistingId_UpdatesAndReturnsRoom()
    {
        var hotelId = await CreateHotelAsync();
        var roomTypeId = await CreateRoomTypeAsync(hotelId);
        var created = await _sut.CreateAsync(roomTypeId, SampleRequest(), CancellationToken.None);

        var updated = await _sut.UpdateAsync(
            created.Room!.Id,
            new RoomRequest { RoomNumber = "102", Status = RoomStatus.Maintenance },
            CancellationToken.None);

        Assert.Equal(RoomSaveOutcome.Success, updated.Outcome);
        Assert.Equal("102", updated.Room!.RoomNumber);
        Assert.Equal(RoomStatus.Maintenance, updated.Room.Status);
    }

    [Fact]
    public async Task UpdateAsync_WithUnknownId_ReturnsParentNotFound()
    {
        var result = await _sut.UpdateAsync(999, SampleRequest(), CancellationToken.None);

        Assert.Equal(RoomSaveOutcome.ParentNotFound, result.Outcome);
    }

    [Fact]
    public async Task UpdateAsync_WithRoomNumberCollidingInSameHotel_ReturnsDuplicateRoomNumber()
    {
        var hotelId = await CreateHotelAsync();
        var roomTypeId = await CreateRoomTypeAsync(hotelId);
        await _sut.CreateAsync(roomTypeId, SampleRequest("101"), CancellationToken.None);
        var second = await _sut.CreateAsync(roomTypeId, SampleRequest("102"), CancellationToken.None);

        var result = await _sut.UpdateAsync(second.Room!.Id, SampleRequest("101"), CancellationToken.None);

        Assert.Equal(RoomSaveOutcome.DuplicateRoomNumber, result.Outcome);
    }

    [Fact]
    public async Task DeleteAsync_WithExistingId_RemovesRoomAndReturnsTrue()
    {
        var hotelId = await CreateHotelAsync();
        var roomTypeId = await CreateRoomTypeAsync(hotelId);
        var created = await _sut.CreateAsync(roomTypeId, SampleRequest(), CancellationToken.None);

        var result = await _sut.DeleteAsync(created.Room!.Id, CancellationToken.None);

        Assert.True(result);
        Assert.Null(await _dbContext.Rooms.FindAsync(created.Room.Id));
    }

    [Fact]
    public async Task DeleteAsync_WithUnknownId_ReturnsFalse()
    {
        var result = await _sut.DeleteAsync(999, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsRoom()
    {
        var hotelId = await CreateHotelAsync();
        var roomTypeId = await CreateRoomTypeAsync(hotelId);
        var created = await _sut.CreateAsync(roomTypeId, SampleRequest(), CancellationToken.None);

        var result = await _sut.GetByIdAsync(created.Room!.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("101", result!.RoomNumber);
    }

    [Fact]
    public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
    {
        var result = await _sut.GetByIdAsync(999, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ListByRoomTypeAsync_ReturnsOnlyThatRoomTypesRooms()
    {
        var hotelId = await CreateHotelAsync();
        var roomTypeAId = await CreateRoomTypeAsync(hotelId, "Standard");
        var roomTypeBId = await CreateRoomTypeAsync(hotelId, "Deluxe");
        await _sut.CreateAsync(roomTypeAId, SampleRequest("101"), CancellationToken.None);
        await _sut.CreateAsync(roomTypeAId, SampleRequest("102"), CancellationToken.None);
        await _sut.CreateAsync(roomTypeBId, SampleRequest("201"), CancellationToken.None);

        var result = await _sut.ListByRoomTypeAsync(roomTypeAId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        Assert.All(result, r => Assert.Equal(roomTypeAId, r.RoomTypeId));
    }

    [Fact]
    public async Task ListByRoomTypeAsync_WithUnknownRoomTypeId_ReturnsNull()
    {
        var result = await _sut.ListByRoomTypeAsync(999, CancellationToken.None);

        Assert.Null(result);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }
}
```

Save as `backend/HotelBookingEngine.Tests/Features/Rooms/RoomServiceTests.cs`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/HotelBookingEngine.Tests --filter RoomServiceTests`
Expected: FAIL (build error — `RoomRequest`/`RoomService`/etc. don't exist yet).

- [ ] **Step 3: Implement the DTOs and result type**

```csharp
using System.Text.Json.Serialization;

namespace HotelBookingEngine.Api.Features.Rooms;

public class RoomRequest
{
    public required string RoomNumber { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RoomStatus Status { get; set; }
}
```

Save as `backend/HotelBookingEngine.Api/Features/Rooms/RoomRequest.cs`.

```csharp
using System.Text.Json.Serialization;

namespace HotelBookingEngine.Api.Features.Rooms;

public record RoomResponse(
    int Id,
    int RoomTypeId,
    int HotelId,
    string RoomNumber,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] RoomStatus Status);
```

Save as `backend/HotelBookingEngine.Api/Features/Rooms/RoomResponse.cs`.

```csharp
namespace HotelBookingEngine.Api.Features.Rooms;

public enum RoomSaveOutcome
{
    Success,
    ParentNotFound,
    DuplicateRoomNumber
}

public record RoomSaveResult(RoomSaveOutcome Outcome, RoomResponse? Room);
```

Save as `backend/HotelBookingEngine.Api/Features/Rooms/RoomSaveResult.cs`.

- [ ] **Step 4: Implement `IRoomService`**

```csharp
namespace HotelBookingEngine.Api.Features.Rooms;

public interface IRoomService
{
    Task<RoomSaveResult> CreateAsync(int roomTypeId, RoomRequest request, CancellationToken cancellationToken);
    Task<RoomSaveResult> UpdateAsync(int id, RoomRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
    Task<RoomResponse?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<List<RoomResponse>?> ListByRoomTypeAsync(int roomTypeId, CancellationToken cancellationToken);
}
```

Save as `backend/HotelBookingEngine.Api/Features/Rooms/IRoomService.cs`.

- [ ] **Step 5: Implement `RoomService`**

```csharp
using HotelBookingEngine.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HotelBookingEngine.Api.Features.Rooms;

public class RoomService : IRoomService
{
    private readonly AppDbContext _dbContext;

    public RoomService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RoomSaveResult> CreateAsync(int roomTypeId, RoomRequest request, CancellationToken cancellationToken)
    {
        var roomType = await _dbContext.RoomTypes.FindAsync([roomTypeId], cancellationToken);
        if (roomType is null)
        {
            return new RoomSaveResult(RoomSaveOutcome.ParentNotFound, null);
        }

        var duplicate = await _dbContext.Rooms.AnyAsync(
            r => r.HotelId == roomType.HotelId && r.RoomNumber == request.RoomNumber, cancellationToken);
        if (duplicate)
        {
            return new RoomSaveResult(RoomSaveOutcome.DuplicateRoomNumber, null);
        }

        var room = new Room
        {
            RoomTypeId = roomTypeId,
            HotelId = roomType.HotelId,
            RoomNumber = request.RoomNumber,
            Status = request.Status
        };

        _dbContext.Rooms.Add(room);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RoomSaveResult(RoomSaveOutcome.Success, ToResponse(room));
    }

    public async Task<RoomSaveResult> UpdateAsync(int id, RoomRequest request, CancellationToken cancellationToken)
    {
        var room = await _dbContext.Rooms.FindAsync([id], cancellationToken);
        if (room is null)
        {
            return new RoomSaveResult(RoomSaveOutcome.ParentNotFound, null);
        }

        var duplicate = await _dbContext.Rooms.AnyAsync(
            r => r.Id != id && r.HotelId == room.HotelId && r.RoomNumber == request.RoomNumber, cancellationToken);
        if (duplicate)
        {
            return new RoomSaveResult(RoomSaveOutcome.DuplicateRoomNumber, null);
        }

        room.RoomNumber = request.RoomNumber;
        room.Status = request.Status;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RoomSaveResult(RoomSaveOutcome.Success, ToResponse(room));
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var room = await _dbContext.Rooms.FindAsync([id], cancellationToken);
        if (room is null)
        {
            return false;
        }

        _dbContext.Rooms.Remove(room);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<RoomResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var room = await _dbContext.Rooms.FindAsync([id], cancellationToken);
        return room is null ? null : ToResponse(room);
    }

    public async Task<List<RoomResponse>?> ListByRoomTypeAsync(int roomTypeId, CancellationToken cancellationToken)
    {
        var roomTypeExists = await _dbContext.RoomTypes.AnyAsync(rt => rt.Id == roomTypeId, cancellationToken);
        if (!roomTypeExists)
        {
            return null;
        }

        var rooms = await _dbContext.Rooms
            .Where(r => r.RoomTypeId == roomTypeId)
            .OrderBy(r => r.RoomNumber)
            .ToListAsync(cancellationToken);

        return rooms.Select(ToResponse).ToList();
    }

    private static RoomResponse ToResponse(Room room) =>
        new(room.Id, room.RoomTypeId, room.HotelId, room.RoomNumber, room.Status);
}
```

Save as `backend/HotelBookingEngine.Api/Features/Rooms/RoomService.cs`. `ListByRoomTypeAsync` materializes rooms with `ToListAsync` before mapping in memory, rather than projecting the enum inside the LINQ-to-Entities query, to avoid depending on SQL translation of an enum-to-string conversion.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test backend/HotelBookingEngine.Tests --filter RoomServiceTests`
Expected: PASS (13 passed).

- [ ] **Step 7: Run the full suite**

Run: `dotnet test backend/HotelBookingEngine.sln`
Expected: all tests pass.

- [ ] **Step 8: Commit**

```bash
git add backend
git commit -m "feat: add RoomService for room CRUD and hotel-scoped room number uniqueness"
```

---

### Task 5: `RoomsController` and DI registration

**Files:**
- Create: `backend/HotelBookingEngine.Api/Features/Rooms/RoomsController.cs`
- Modify: `backend/HotelBookingEngine.Api/Extensions/ApplicationServicesCollectionExtensions.cs`

**Interfaces:**
- Consumes: `IRoomService` (Task 4).
- Produces: `GET`/`POST /api/room-types/{roomTypeId}/rooms`, `GET`/`PUT`/`DELETE /api/rooms/{id}` — Task 6's integration tests and Tasks 7-9's frontend both call these exact routes.

- [ ] **Step 1: Implement `RoomsController`**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelBookingEngine.Api.Features.Rooms;

[ApiController]
[Authorize]
public class RoomsController : ControllerBase
{
    private readonly IRoomService _roomService;

    public RoomsController(IRoomService roomService)
    {
        _roomService = roomService;
    }

    [HttpGet("api/room-types/{roomTypeId:int}/rooms")]
    public async Task<ActionResult<List<RoomResponse>>> ListByRoomType(int roomTypeId, CancellationToken cancellationToken)
    {
        var rooms = await _roomService.ListByRoomTypeAsync(roomTypeId, cancellationToken);
        return rooms is null ? NotFound() : Ok(rooms);
    }

    [HttpPost("api/room-types/{roomTypeId:int}/rooms")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RoomResponse>> Create(int roomTypeId, RoomRequest request, CancellationToken cancellationToken)
    {
        var result = await _roomService.CreateAsync(roomTypeId, request, cancellationToken);
        return result.Outcome switch
        {
            RoomSaveOutcome.Success => CreatedAtAction(nameof(GetById), new { id = result.Room!.Id }, result.Room),
            RoomSaveOutcome.ParentNotFound => NotFound(),
            RoomSaveOutcome.DuplicateRoomNumber => Conflict("A room with this number already exists in this hotel."),
            _ => throw new InvalidOperationException($"Unhandled {nameof(RoomSaveOutcome)} value: {result.Outcome}")
        };
    }

    [HttpGet("api/rooms/{id:int}")]
    public async Task<ActionResult<RoomResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var room = await _roomService.GetByIdAsync(id, cancellationToken);
        return room is null ? NotFound() : Ok(room);
    }

    [HttpPut("api/rooms/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RoomResponse>> Update(int id, RoomRequest request, CancellationToken cancellationToken)
    {
        var result = await _roomService.UpdateAsync(id, request, cancellationToken);
        return result.Outcome switch
        {
            RoomSaveOutcome.Success => Ok(result.Room),
            RoomSaveOutcome.ParentNotFound => NotFound(),
            RoomSaveOutcome.DuplicateRoomNumber => Conflict("A room with this number already exists in this hotel."),
            _ => throw new InvalidOperationException($"Unhandled {nameof(RoomSaveOutcome)} value: {result.Outcome}")
        };
    }

    [HttpDelete("api/rooms/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _roomService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
```

Save as `backend/HotelBookingEngine.Api/Features/Rooms/RoomsController.cs`. No class-level `[Route]` — the two URL shapes (`api/room-types/{roomTypeId}/rooms` and `api/rooms/{id}`) coexist on absolute per-action route templates instead.

- [ ] **Step 2: Register `IRoomService`**

Replace the contents of `backend/HotelBookingEngine.Api/Extensions/ApplicationServicesCollectionExtensions.cs` with:

```csharp
using HotelBookingEngine.Api.Features.Auth;
using HotelBookingEngine.Api.Features.Health;
using HotelBookingEngine.Api.Features.Hotels;
using HotelBookingEngine.Api.Features.RoomTypes;
using HotelBookingEngine.Api.Features.Rooms;
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
        services.AddScoped<IRoomService, RoomService>();

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

Copy the `token`, create a hotel, note its `id`, create a room type under it, note its `id`, then:

```bash
curl -i -X POST http://localhost:5058/api/room-types/1/rooms -H "Content-Type: application/json" -H "Authorization: Bearer <token>" -d "{\"roomNumber\":\"101\",\"status\":\"Available\"}"
```

Expected: `201 Created` with the room body including `id`, `roomTypeId`, and `hotelId`. Then:

```bash
curl -i http://localhost:5058/api/room-types/1/rooms -H "Authorization: Bearer <token>"
```

Expected: `200` with a JSON array containing the created room. Then:

```bash
curl -i http://localhost:5058/api/room-types/999/rooms -H "Authorization: Bearer <token>"
```

Expected: `404` (unknown room type id). Then repeat the create request with the same `roomNumber` under the same hotel (a different room type in that hotel, if you created one) — expected: `409`. Stop the process once confirmed.

- [ ] **Step 4: Commit**

```bash
git add backend
git commit -m "feat: add RoomsController with role-restricted write endpoints"
```

---

### Task 6: Integration tests proving role enforcement, room-type-scoping, and duplicate detection

**Files:**
- Create: `backend/HotelBookingEngine.Tests/Features/Rooms/RoomsEndpointsTests.cs`

**Interfaces:**
- Consumes: `Program`, `AppDbContext`, `User`/`Role` (Phase 2), `POST /api/auth/login`, `POST /api/hotels`, `POST /api/hotels/{hotelId}/room-types`, all `/api/room-types/{roomTypeId}/rooms` and `/api/rooms/{id}` routes (Task 5).

- [ ] **Step 1: Write the tests**

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HotelBookingEngine.Api.Features.Auth;
using HotelBookingEngine.Api.Features.Hotels;
using HotelBookingEngine.Api.Features.RoomTypes;
using HotelBookingEngine.Api.Features.Rooms;
using HotelBookingEngine.Api.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace HotelBookingEngine.Tests.Features.Rooms;

public class RoomsEndpointsTests : IDisposable
{
    private const string ReceptionistUsername = "receptionist-test";
    private const string ReceptionistPassword = "Reception123!";

    private readonly string _dbPath;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public RoomsEndpointsTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"hotelbookingengine-rooms-tests-{Guid.NewGuid():N}.db");

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

    private async Task<int> CreateHotelAsync(string name = "Grand Hotel")
    {
        var response = await _client.PostAsJsonAsync("/api/hotels", new
        {
            Name = name,
            Address = "123 Main St",
            City = "Springfield",
            Phone = "555-0100"
        });
        var hotel = await response.Content.ReadFromJsonAsync<HotelResponse>();
        return hotel!.Id;
    }

    private async Task<int> CreateRoomTypeAsync(int hotelId, string name = "Deluxe")
    {
        var response = await _client.PostAsJsonAsync($"/api/hotels/{hotelId}/room-types", new
        {
            Name = name,
            Description = "Spacious room with a view",
            Capacity = 2,
            DailyRate = 150m
        });
        var roomType = await response.Content.ReadFromJsonAsync<RoomTypeResponse>();
        return roomType!.Id;
    }

    private static object SampleRequestBody(string roomNumber = "101") => new
    {
        RoomNumber = roomNumber,
        Status = "Available"
    };

    [Fact]
    public async Task ListByRoomType_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/room-types/1/rooms");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_AsAdmin_ReturnsOkWithRoom()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();
        var roomTypeId = await CreateRoomTypeAsync(hotelId);
        var created = await (await _client.PostAsJsonAsync($"/api/room-types/{roomTypeId}/rooms", SampleRequestBody()))
            .Content.ReadFromJsonAsync<RoomResponse>();

        var response = await _client.GetAsync($"/api/rooms/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var room = await response.Content.ReadFromJsonAsync<RoomResponse>();
        Assert.Equal(created.Id, room!.Id);
        Assert.Equal(created.RoomNumber, room.RoomNumber);
    }

    [Fact]
    public async Task ListByRoomType_WithUnknownRoomTypeId_ReturnsNotFound()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));

        var response = await _client.GetAsync("/api/room-types/999/rooms");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithUnknownRoomTypeId_ReturnsNotFound()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));

        var response = await _client.PostAsJsonAsync("/api/room-types/999/rooms", SampleRequestBody());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListByRoomType_AsReceptionist_ReturnsOk()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();
        var roomTypeId = await CreateRoomTypeAsync(hotelId);

        AuthorizeAs(await LoginAsync(ReceptionistUsername, ReceptionistPassword));
        var response = await _client.GetAsync($"/api/room-types/{roomTypeId}/rooms");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsReceptionist_ReturnsForbidden()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();
        var roomTypeId = await CreateRoomTypeAsync(hotelId);

        AuthorizeAs(await LoginAsync(ReceptionistUsername, ReceptionistPassword));
        var response = await _client.PostAsJsonAsync($"/api/room-types/{roomTypeId}/rooms", SampleRequestBody());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsAdmin_ReturnsCreatedWithHotelIdAndThenListIncludesIt()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();
        var roomTypeId = await CreateRoomTypeAsync(hotelId);

        var createResponse = await _client.PostAsJsonAsync($"/api/room-types/{roomTypeId}/rooms", SampleRequestBody());
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<RoomResponse>();
        Assert.NotNull(created);
        Assert.Equal("101", created!.RoomNumber);
        Assert.Equal(roomTypeId, created.RoomTypeId);
        Assert.Equal(hotelId, created.HotelId);

        var listResponse = await _client.GetAsync($"/api/room-types/{roomTypeId}/rooms");
        var rooms = await listResponse.Content.ReadFromJsonAsync<List<RoomResponse>>();
        Assert.Contains(rooms!, r => r.Id == created.Id);
    }

    [Fact]
    public async Task Create_WithDuplicateRoomNumberInSameHotel_ReturnsConflict()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();
        var roomTypeAId = await CreateRoomTypeAsync(hotelId, "Standard");
        var roomTypeBId = await CreateRoomTypeAsync(hotelId, "Deluxe");
        await _client.PostAsJsonAsync($"/api/room-types/{roomTypeAId}/rooms", SampleRequestBody("101"));

        var response = await _client.PostAsJsonAsync($"/api/room-types/{roomTypeBId}/rooms", SampleRequestBody("101"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_AsReceptionist_ReturnsForbidden()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();
        var roomTypeId = await CreateRoomTypeAsync(hotelId);
        var created = await (await _client.PostAsJsonAsync($"/api/room-types/{roomTypeId}/rooms", SampleRequestBody()))
            .Content.ReadFromJsonAsync<RoomResponse>();

        AuthorizeAs(await LoginAsync(ReceptionistUsername, ReceptionistPassword));
        var response = await _client.PutAsJsonAsync($"/api/rooms/{created!.Id}", SampleRequestBody("102"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_AsAdmin_ReturnsOkWithUpdatedData()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();
        var roomTypeId = await CreateRoomTypeAsync(hotelId);
        var created = await (await _client.PostAsJsonAsync($"/api/room-types/{roomTypeId}/rooms", SampleRequestBody()))
            .Content.ReadFromJsonAsync<RoomResponse>();

        var response = await _client.PutAsJsonAsync($"/api/rooms/{created!.Id}", SampleRequestBody("102"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<RoomResponse>();
        Assert.Equal("102", updated!.RoomNumber);
        Assert.Equal(created.Id, updated.Id);
    }

    [Fact]
    public async Task Delete_AsReceptionist_ReturnsForbidden()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();
        var roomTypeId = await CreateRoomTypeAsync(hotelId);
        var created = await (await _client.PostAsJsonAsync($"/api/room-types/{roomTypeId}/rooms", SampleRequestBody()))
            .Content.ReadFromJsonAsync<RoomResponse>();

        AuthorizeAs(await LoginAsync(ReceptionistUsername, ReceptionistPassword));
        var response = await _client.DeleteAsync($"/api/rooms/{created!.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsAdmin_RemovesRoom()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();
        var roomTypeId = await CreateRoomTypeAsync(hotelId);
        var created = await (await _client.PostAsJsonAsync($"/api/room-types/{roomTypeId}/rooms", SampleRequestBody()))
            .Content.ReadFromJsonAsync<RoomResponse>();

        var deleteResponse = await _client.DeleteAsync($"/api/rooms/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/api/rooms/{created.Id}");
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

Save as `backend/HotelBookingEngine.Tests/Features/Rooms/RoomsEndpointsTests.cs`.

- [ ] **Step 2: Run the tests**

Run: `dotnet test backend/HotelBookingEngine.Tests --filter RoomsEndpointsTests`
Expected: PASS (12 passed).

- [ ] **Step 3: Run the full suite**

Run: `dotnet test backend/HotelBookingEngine.sln`
Expected: all tests pass, pristine output.

- [ ] **Step 4: Commit**

```bash
git add backend
git commit -m "test: add integration tests for room endpoints"
```

---

### Task 7: Room types and frontend API service

**Files:**
- Create: `frontend/src/types/room.ts`
- Create: `frontend/src/features/rooms/roomService.ts`

**Interfaces:**
- Produces: `Room { id, roomTypeId, hotelId, roomNumber, status }`, `RoomRequest { roomNumber, status }`, `RoomStatus` union type, `listRooms(roomTypeId)`, `getRoom(id)`, `createRoom(roomTypeId, request)`, `updateRoom(id, request)`, `deleteRoom(id)`. Tasks 8-9 consume these.

- [ ] **Step 1: Create the types**

```typescript
export type RoomStatus = "Available" | "Maintenance";

export interface Room {
  id: number;
  roomTypeId: number;
  hotelId: number;
  roomNumber: string;
  status: RoomStatus;
}

export interface RoomRequest {
  roomNumber: string;
  status: RoomStatus;
}
```

Save as `frontend/src/types/room.ts`.

- [ ] **Step 2: Create the API service**

```typescript
import { httpClient } from "../../api/httpClient";
import type { Room, RoomRequest } from "../../types/room";

export async function listRooms(roomTypeId: number): Promise<Room[]> {
  const response = await httpClient.get<Room[]>(`/api/room-types/${roomTypeId}/rooms`);
  return response.data;
}

export async function getRoom(id: number): Promise<Room> {
  const response = await httpClient.get<Room>(`/api/rooms/${id}`);
  return response.data;
}

export async function createRoom(roomTypeId: number, request: RoomRequest): Promise<Room> {
  const response = await httpClient.post<Room>(`/api/room-types/${roomTypeId}/rooms`, request);
  return response.data;
}

export async function updateRoom(id: number, request: RoomRequest): Promise<Room> {
  const response = await httpClient.put<Room>(`/api/rooms/${id}`, request);
  return response.data;
}

export async function deleteRoom(id: number): Promise<void> {
  await httpClient.delete(`/api/rooms/${id}`);
}
```

Save as `frontend/src/features/rooms/roomService.ts`.

- [ ] **Step 3: Verify**

```bash
cd frontend && npm run build
```

Expected: 0 TypeScript errors.

- [ ] **Step 4: Commit**

```bash
git add frontend
git commit -m "feat: add room types and API service"
```

---

### Task 8: Room Type Rooms list page, routing, and the Room Types page delete-conflict handler

**Files:**
- Create: `frontend/src/pages/RoomTypeRoomsPage.tsx`
- Modify: `frontend/src/routes/AppRoutes.tsx`
- Modify: `frontend/src/pages/HotelRoomTypesPage.tsx`

**Interfaces:**
- Consumes: `listRooms`, `deleteRoom` (Task 7), `getRoomType` (Phase 4), `getHotel` (Phase 3), `ProtectedRoute` (Phase 3), `useAuth()` (Phase 2).
- Produces: route `/hotels/:hotelId/room-types/:roomTypeId/rooms`.

- [ ] **Step 1: Create the Room Type Rooms list page**

```typescript
import { Link, useParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { deleteRoom, listRooms } from "../features/rooms/roomService";
import { getRoomType } from "../features/roomTypes/roomTypeService";
import { getHotel } from "../features/hotels/hotelService";
import { useAuth } from "../hooks/useAuth";

export function RoomTypeRoomsPage() {
  const { hotelId: hotelIdParam, roomTypeId: roomTypeIdParam } = useParams<{ hotelId: string; roomTypeId: string }>();
  const hotelId = Number(hotelIdParam);
  const roomTypeId = Number(roomTypeIdParam);
  const { user } = useAuth();
  const queryClient = useQueryClient();

  const { data: hotel } = useQuery({
    queryKey: ["hotels", hotelId],
    queryFn: () => getHotel(hotelId),
  });

  const { data: roomType } = useQuery({
    queryKey: ["room-types", roomTypeId],
    queryFn: () => getRoomType(roomTypeId),
  });

  const { data, isLoading, isError } = useQuery({
    queryKey: ["room-types", roomTypeId, "rooms"],
    queryFn: () => listRooms(roomTypeId),
  });

  const deleteMutation = useMutation({
    mutationFn: deleteRoom,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["room-types", roomTypeId, "rooms"] });
    },
  });

  const isAdmin = user?.role === "Admin";

  function handleDelete(id: number, roomNumber: string) {
    if (window.confirm(`Delete room "${roomNumber}"?`)) {
      deleteMutation.mutate(id);
    }
  }

  return (
    <main className="flex min-h-screen flex-col items-center gap-4 p-8">
      <Link to={`/hotels/${hotelId}/room-types`} className="text-blue-600 underline">
        Back to Room Types
      </Link>
      <h1 className="text-2xl font-semibold">
        Rooms{roomType ? ` — ${roomType.name}` : ""}{hotel ? ` (${hotel.name})` : ""}
      </h1>

      {isAdmin && (
        <Link to={`/hotels/${hotelId}/room-types/${roomTypeId}/rooms/new`} className="text-blue-600 underline">
          New Room
        </Link>
      )}

      {isLoading && <p>Loading rooms...</p>}
      {isError && <p>Unable to load rooms.</p>}

      {data && (
        <table className="w-full max-w-3xl border-collapse">
          <thead>
            <tr className="border-b text-left">
              <th className="p-2">Room Number</th>
              <th className="p-2">Status</th>
              {isAdmin && <th className="p-2">Actions</th>}
            </tr>
          </thead>
          <tbody>
            {data.map((room) => (
              <tr key={room.id} className="border-b">
                <td className="p-2">{room.roomNumber}</td>
                <td className="p-2">{room.status}</td>
                {isAdmin && (
                  <td className="flex gap-2 p-2">
                    <Link
                      to={`/hotels/${hotelId}/room-types/${roomTypeId}/rooms/${room.id}/edit`}
                      className="text-blue-600 underline"
                    >
                      Edit
                    </Link>
                    <button
                      onClick={() => handleDelete(room.id, room.roomNumber)}
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

Save as `frontend/src/pages/RoomTypeRoomsPage.tsx`.

- [ ] **Step 2: Add the `/hotels/:hotelId/room-types/:roomTypeId/rooms` route**

Replace the contents of `frontend/src/routes/AppRoutes.tsx` with:

```typescript
import { Routes, Route } from "react-router-dom";
import { HomePage } from "../pages/HomePage";
import { LoginPage } from "../pages/LoginPage";
import { HotelsPage } from "../pages/HotelsPage";
import { HotelFormPage } from "../pages/HotelFormPage";
import { HotelRoomTypesPage } from "../pages/HotelRoomTypesPage";
import { RoomTypeFormPage } from "../pages/RoomTypeFormPage";
import { RoomTypeRoomsPage } from "../pages/RoomTypeRoomsPage";
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
      <Route
        path="/hotels/:hotelId/room-types/:roomTypeId/rooms"
        element={
          <ProtectedRoute>
            <RoomTypeRoomsPage />
          </ProtectedRoute>
        }
      />
    </Routes>
  );
}
```

- [ ] **Step 3: Add a "Rooms" link per row and the `onError` handler to `HotelRoomTypesPage`**

Replace the contents of `frontend/src/pages/HotelRoomTypesPage.tsx` with:

```typescript
import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { isAxiosError } from "axios";
import { deleteRoomType, listRoomTypes } from "../features/roomTypes/roomTypeService";
import { getHotel } from "../features/hotels/hotelService";
import { useAuth } from "../hooks/useAuth";

export function HotelRoomTypesPage() {
  const { hotelId: hotelIdParam } = useParams<{ hotelId: string }>();
  const hotelId = Number(hotelIdParam);
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const [deleteError, setDeleteError] = useState<string | null>(null);

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
      setDeleteError(null);
      queryClient.invalidateQueries({ queryKey: ["hotels", hotelId, "room-types"] });
    },
    onError: (error) => {
      const message =
        isAxiosError(error) && typeof error.response?.data === "string"
          ? error.response.data
          : "Unable to delete this room type.";
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
      {deleteError && <p className="text-sm text-red-600">{deleteError}</p>}

      {data && (
        <table className="w-full max-w-3xl border-collapse">
          <thead>
            <tr className="border-b text-left">
              <th className="p-2">Name</th>
              <th className="p-2">Description</th>
              <th className="p-2">Capacity</th>
              <th className="p-2">Daily Rate</th>
              <th className="p-2">Rooms</th>
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
                <td className="p-2">
                  <Link to={`/hotels/${hotelId}/room-types/${roomType.id}/rooms`} className="text-blue-600 underline">
                    Rooms
                  </Link>
                </td>
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

- [ ] **Step 4: Verify**

```bash
cd frontend && npm run build
```

Expected: 0 TypeScript errors.

- [ ] **Step 5: Commit**

```bash
git add frontend
git commit -m "feat: add room type rooms list page and room type delete conflict message"
```

---

### Task 9: Room create/edit form, remaining routes, manual verification, README

**Files:**
- Create: `frontend/src/pages/RoomFormPage.tsx`
- Modify: `frontend/src/routes/AppRoutes.tsx`
- Modify: `README.md`

**Interfaces:**
- Consumes: `createRoom`, `updateRoom`, `getRoom` (Task 7), `ProtectedRoute` (Phase 3).
- Produces: routes `/hotels/:hotelId/room-types/:roomTypeId/rooms/new`, `/hotels/:hotelId/room-types/:roomTypeId/rooms/:id/edit`.

- [ ] **Step 1: Create the Room form page**

```typescript
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useNavigate, useParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { isAxiosError } from "axios";
import { createRoom, getRoom, updateRoom } from "../features/rooms/roomService";

const roomSchema = z.object({
  roomNumber: z.string().min(1, "Room number is required").max(20),
  status: z.enum(["Available", "Maintenance"]),
});

type RoomFormValues = z.infer<typeof roomSchema>;

export function RoomFormPage() {
  const {
    hotelId: hotelIdParam,
    roomTypeId: roomTypeIdParam,
    id,
  } = useParams<{ hotelId: string; roomTypeId: string; id: string }>();
  const hotelId = Number(hotelIdParam);
  const roomTypeId = Number(roomTypeIdParam);
  const roomId = id ? Number(id) : undefined;
  const isEditMode = roomId !== undefined;
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [saveError, setSaveError] = useState<string | null>(null);

  const { data: existingRoom } = useQuery({
    queryKey: ["rooms", roomId],
    queryFn: () => getRoom(roomId!),
    enabled: isEditMode,
  });

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<RoomFormValues>({
    resolver: zodResolver(roomSchema),
    defaultValues: { status: "Available" },
  });

  useEffect(() => {
    if (existingRoom && isEditMode) {
      if (existingRoom.roomTypeId !== roomTypeId) {
        navigate(`/hotels/${hotelId}/room-types/${existingRoom.roomTypeId}/rooms`, { replace: true });
        return;
      }

      reset({
        roomNumber: existingRoom.roomNumber,
        status: existingRoom.status,
      });
    }
  }, [existingRoom, isEditMode, roomTypeId, hotelId, navigate, reset]);

  const mutation = useMutation({
    mutationFn: (values: RoomFormValues) =>
      isEditMode ? updateRoom(roomId!, values) : createRoom(roomTypeId, values),
    onSuccess: () => {
      setSaveError(null);
      queryClient.invalidateQueries({ queryKey: ["room-types", roomTypeId, "rooms"] });
      if (roomId !== undefined) {
        queryClient.invalidateQueries({ queryKey: ["rooms", roomId] });
      }
      navigate(`/hotels/${hotelId}/room-types/${roomTypeId}/rooms`);
    },
    onError: (error) => {
      const message =
        isAxiosError(error) && typeof error.response?.data === "string"
          ? error.response.data
          : "Unable to save this room.";
      setSaveError(message);
    },
  });

  function onSubmit(values: RoomFormValues) {
    mutation.mutate(values);
  }

  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-4">
      <h1 className="text-2xl font-semibold">{isEditMode ? "Edit Room" : "New Room"}</h1>
      <form onSubmit={handleSubmit(onSubmit)} className="flex w-full max-w-sm flex-col gap-3">
        <div>
          <label htmlFor="roomNumber" className="block text-sm font-medium">
            Room Number
          </label>
          <input
            id="roomNumber"
            type="text"
            className="w-full rounded border px-3 py-2"
            {...register("roomNumber")}
          />
          {errors.roomNumber && <p className="text-sm text-red-600">{errors.roomNumber.message}</p>}
        </div>
        <div>
          <label htmlFor="status" className="block text-sm font-medium">
            Status
          </label>
          <select id="status" className="w-full rounded border px-3 py-2" {...register("status")}>
            <option value="Available">Available</option>
            <option value="Maintenance">Maintenance</option>
          </select>
          {errors.status && <p className="text-sm text-red-600">{errors.status.message}</p>}
        </div>
        {saveError && <p className="text-sm text-red-600">{saveError}</p>}
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

Save as `frontend/src/pages/RoomFormPage.tsx`. The `roomTypeId` mismatch redirect mirrors the fix already applied to `RoomTypeFormPage` in Phase 4 — navigating directly to an edit URL whose `roomTypeId` doesn't match the room's actual room type sends the user to the correct list instead of silently saving under the wrong parent.

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
import { RoomTypeRoomsPage } from "../pages/RoomTypeRoomsPage";
import { RoomFormPage } from "../pages/RoomFormPage";
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
      <Route
        path="/hotels/:hotelId/room-types/:roomTypeId/rooms"
        element={
          <ProtectedRoute>
            <RoomTypeRoomsPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/hotels/:hotelId/room-types/:roomTypeId/rooms/new"
        element={
          <ProtectedRoute roles={["Admin"]}>
            <RoomFormPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/hotels/:hotelId/room-types/:roomTypeId/rooms/:id/edit"
        element={
          <ProtectedRoute roles={["Admin"]}>
            <RoomFormPage />
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
1. Log in as `admin`/`Admin123!`. Go to `/hotels`, click "Room Types" on a hotel row, then click "Rooms" on a room type row → `/hotels/:hotelId/room-types/:roomTypeId/rooms` loads, empty list, "New Room" link visible (Admin).
2. Click "New Room" → fill the form (room number + status) → Save → redirected to the rooms list, the new room appears in the table.
3. Click "Edit" on that row → form pre-filled with current values → change the room number → Save → redirected back, table shows the updated number.
4. Create a second room type under the same hotel, try to create a room there with the same room number as step 2's room → the save fails and the form shows an error message (the new `409` handling).
5. Click "Delete" on the first room → confirm dialog → row disappears from the table.
6. Go back to the room types list, try to "Delete" the room type that still has the second room from step 4 → the delete fails and the page shows an error message (the new `409` handling). Delete that room first, then delete the room type — it now succeeds.
7. Navigate directly to `/hotels/1/room-types/1/rooms/new` while logged out → redirected to `/login`.
8. (Optional, requires a second account) Confirm a Receptionist-role user sees the rooms table without "New Room"/"Edit"/"Delete" controls, and is redirected away from the form routes if navigated to directly. This exact enforcement is already proven by Task 6's integration tests at the API level.

Stop both processes once confirmed. This step needs a human/browser to actually execute — describe the result when reporting back rather than assuming it passed.

- [ ] **Step 5: Update the README**

Update the "Status" line in `README.md` to: `Phase 1, Phase 2 (Authentication), Phase 3 (Hotels), Phase 4 (Room Types), and Phase 5 (Rooms) complete. Next: Phase 6 — Guests.`

- [ ] **Step 6: Commit**

```bash
git add frontend README.md
git commit -m "feat: add room create/edit form and wire remaining routes"
```

---

## Self-Review Notes

- **Spec coverage:** Create/Update/Delete/List Room (Tasks 1, 4-9), `Room` scoped to a `RoomType` via required `RoomTypeId` and denormalized `HotelId` derived automatically from the room type (Tasks 1, 4), hotel-scoped `RoomNumber` uniqueness enforced in `RoomService` (Task 4), `RoomStatus` as a two-value enum bound via `JsonStringEnumConverter` (Tasks 1, 4), Restrict-not-cascade on room type delete with the `RoomTypeDeleteResult` contract change (Tasks 2-3), the explicit no-new-`HotelService`-check reasoning for `Room`→`Hotel` Restrict (Global Constraints, Task 1), the two-URL-shape routing on one controller (Task 5), `RoomSaveResult`/`RoomSaveOutcome` distinguishing parent-not-found from duplicate-room-number on both create and update (Tasks 4-6), authorization mirroring Hotels/RoomTypes (Tasks 5-6), frontend nested routes and `HotelRoomTypesPage`'s "Rooms" link (Task 8), the `HotelRoomTypesPage` delete `onError` handler closing the Phase 4-deferred gap (Task 8), the `RoomFormPage` save `onError` handler and roomType-mismatch redirect (Task 9) are all covered. Guests/Reservations/Dashboard, re-parenting UI, and any hotel-wide (non-room-type-scoped) room view are untouched, as required.
- **Placeholder scan:** no TODO/TBD; all code blocks are complete and consistent with the established Phase 1-4 patterns (SQLite-backed unit tests, `WebApplicationFactory` integration tests, manual DTO mapping, thin controllers, `mutation.isPending` for submit-button disabling, `isAxiosError` for typed error narrowing).
- **Type consistency:** `RoomResponse` (C#: `Id, RoomTypeId, HotelId, RoomNumber, Status`) matches TS `Room` (`id, roomTypeId, hotelId, roomNumber, status`) via ASP.NET Core's default camelCase policy, with `Status` serializing as the string enum name on both sides via `JsonStringEnumConverter` / the TS `RoomStatus` union. `RoomRequest`/`IRoomService` signatures are used identically across Tasks 4-6. `RoomSaveOutcome` values (`Success`/`ParentNotFound`/`DuplicateRoomNumber`) are used identically in `RoomService` (Task 4), `RoomsController` (Task 5), and their respective tests. `RoomTypeDeleteResult` values (`Deleted`/`NotFound`/`HasRooms`) are used identically in `RoomTypeService` (Task 2), `RoomTypesController` (Task 3), and their tests. `listRooms(roomTypeId)`/`getRoom(id)`/`createRoom(roomTypeId, request)`/`updateRoom(id, request)`/`deleteRoom(id)` signatures from Task 7 are called identically in Tasks 8-9.
