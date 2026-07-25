# Phase 4 — Room Types Design

## Context

Phase 3 delivered Hotels CRUD with role-restricted writes (`Admin` only) and reads open to any authenticated user — the first vertical slice with real business data. Phase 4, per the MVP Development Order in `prompts/project-01.md`, is Room Types: Create, Update, Delete, List. Unlike Hotels, Room Types don't exist in isolation — they belong to a specific hotel, which is the first parent/child relationship in the data model and the first time a delete on one entity needs to consider another.

The spec's field grouping literally puts `Capacity` and `Daily Rate` under **Rooms** (Phase 5), not Room Types. This document records a deliberate deviation from that literal grouping, plus the relationship, delete-safety, and routing decisions the spec leaves open.

## Decisions

- **`RoomType` belongs to a specific `Hotel`** via a required `HotelId` foreign key — not a global catalog. Two hotels can each have their own "Deluxe" room type as separate rows. This also sets up Phase 5: `Room` will belong to a `RoomType` (and transitively to a `Hotel`) the same way.
- **`Capacity` and `Daily Rate` move to `RoomType`, not `Room`** — a deliberate deviation from the spec's literal field grouping (which lists them under Rooms). Pricing and capacity by room *category* is the more common real-world hotel model than per-physical-room pricing. **Consequence for Phase 5**: `Room` will most likely only need `RoomNumber`, `RoomTypeId`, and `Status` — capacity and rate are inherited from the room type, not duplicated per room. This will be confirmed again when Phase 5 is designed, not assumed silently.
- **Fields**: `Name` (required, max 100 — e.g. "Standard", "Deluxe"), `Description` (required, max 500), `Capacity` (required, integer, must be > 0), `DailyRate` (required, decimal, must be > 0).
- **Deleting a `Hotel` that still has `RoomType`s is blocked (Restrict), not cascaded.** `DELETE /api/hotels/{id}` returns `409 Conflict` if any room types reference it, forcing the room types to be deleted first. Chosen over cascade to avoid silent, surprising data loss — a real hotel system shouldn't let "delete hotel" quietly wipe out its room categories (and later, once Phase 5 lands, its rooms).
- **This requires changing `IHotelService.DeleteAsync`'s contract** (Phase 3 code) from `Task<bool>` to a small result enum (`Deleted`/`NotFound`/`HasRoomTypes`), since a plain bool can no longer distinguish "not found" from "blocked by dependents." `HotelsController.Delete` maps the three outcomes to `204`/`404`/`409` respectively. The two existing `HotelServiceTests` delete tests are updated to the new contract; a new test covers the conflict case.
- **Routing mirrors the parent/child relationship**: `GET`/`POST` live under `/api/hotels/{hotelId}/room-types` (list and create are inherently hotel-scoped); `GET`/`PUT`/`DELETE` by id live under the flat `/api/room-types/{id}` (an individual room type's id is already globally unique, no need to repeat the hotel in the URL for those). One controller, explicit per-action route templates (no class-level `[Route]`, since the two URL shapes coexist) — not two separate controller classes for one resource.
- **`ListByHotelAsync`/`CreateAsync` return `null` specifically to mean "the hotel doesn't exist"** (→ `404`), distinct from an empty list (a real hotel with zero room types yet, → `200` with `[]`). Matches the `null`-means-not-found convention already established by `HotelService`.
- **Authorization mirrors Hotels exactly**: `GET` requires only `[Authorize]` (any role); `POST`/`PUT`/`DELETE` require `[Authorize(Roles="Admin")]` in addition.
- **Frontend routes nest under the hotel** (`/hotels/:hotelId/room-types`, `/room-types/new`, `/:id/edit`), matching the API shape. `HotelsPage` gains a "Room Types" link per row, visible to any authenticated user (reads are open).
- **Closing a gap from Phase 3's final review**: `HotelsPage`'s delete mutation gets an `onError` handler now, because deleting a hotel with existing room types is a real, expected failure mode in this phase (the backend's new `409`) — previously there was no error feedback on hotel-delete failure at all; this phase is the first time that gap actually matters.

## Backend Design

### Data model

`Features/RoomTypes/RoomType.cs`:

| Field | Type | Rule |
|---|---|---|
| `Id` | `int` (identity) | PK |
| `HotelId` | `int` | required FK → `Hotel.Id` |
| `Name` | `string` | required, max length 100 |
| `Description` | `string` | required, max length 500 |
| `Capacity` | `int` | required, > 0 |
| `DailyRate` | `decimal` | required, > 0 |

`RoomTypeConfiguration : IEntityTypeConfiguration<RoomType>` sets the above plus the FK relationship, with `DeleteBehavior.Restrict` explicitly configured on the EF relationship itself as defense-in-depth beneath the application-layer check in `HotelService`. This matters because `Microsoft.Data.Sqlite` enforces FK constraints by default (`PRAGMA foreign_keys=ON`), so without an explicit `Restrict`, EF Core's default cascade behavior for a required relationship would silently delete room types if any future code path ever removed a `Hotel` without going through `HotelService.DeleteAsync`'s guard.

### Feature slice — `Features/RoomTypes/`

| File | Responsibility |
|---|---|
| `RoomType.cs`, `RoomTypeConfiguration.cs` | entity, Fluent API |
| `RoomTypeRequest.cs` | shared Create/Update DTO (`Name`, `Description`, `Capacity`, `DailyRate` — no `HotelId`, supplied by the route) |
| `RoomTypeResponse.cs` | response DTO (includes `HotelId`) |
| `IRoomTypeService.cs` / `RoomTypeService.cs` | `ListByHotelAsync(hotelId)` / `CreateAsync(hotelId, request)` → `null` if the hotel doesn't exist; `UpdateAsync`/`GetByIdAsync` → `null` if the room type doesn't exist; `DeleteAsync` → `bool` |
| `RoomTypesController.cs` | one controller, absolute per-action routes, thin (delegates only) |

### Endpoints

| Route | Verb | Authorization | Notes |
|---|---|---|---|
| `/api/hotels/{hotelId}/room-types` | GET | `[Authorize]` | `404` if hotel doesn't exist |
| `/api/hotels/{hotelId}/room-types` | POST | `[Authorize(Roles="Admin")]` | `404` if hotel doesn't exist |
| `/api/room-types/{id}` | GET | `[Authorize]` | `404` if not found |
| `/api/room-types/{id}` | PUT | `[Authorize(Roles="Admin")]` | `404` if not found |
| `/api/room-types/{id}` | DELETE | `[Authorize(Roles="Admin")]` | `404` if not found |

### Change to existing Hotels code (Phase 3)

`IHotelService.DeleteAsync` changes from `Task<bool>` to `Task<HotelDeleteResult>`:

```csharp
public enum HotelDeleteResult { Deleted, NotFound, HasRoomTypes }
```

`HotelService.DeleteAsync` checks for any `RoomType` referencing the hotel before deleting; `HotelsController.Delete` maps `Deleted → 204`, `NotFound → 404`, `HasRoomTypes → 409` (with a message body explaining why).

### Testing

- `RoomTypeServiceTests` (unit, SQLite-backed, same pattern as `HotelServiceTests`): create with a valid hotel id persists and returns the correct response; create with an unknown hotel id returns `null`; update/delete/get-by-id existing vs. unknown id; `ListByHotelAsync` returns only the requested hotel's room types (verified by creating room types under two different hotels) and returns `null` for an unknown hotel id.
- `HotelServiceTests` updates: the two existing delete tests move to the new enum contract; one new test proves a hotel with an existing room type returns `HasRoomTypes` and is NOT actually deleted.
- `RoomTypesEndpointsTests` (integration, `WebApplicationFactory<Program>`, same pattern as `HotelsEndpointsTests`): list/create with an unknown hotel id → `404`; Receptionist → `200` on reads, `403` on writes; Admin → success on all, including the create→list round trip.
- `HotelsEndpointsTests` addition: one integration test proving `DELETE /api/hotels/{id}` returns a real `409` when the hotel has an existing room type.

## Frontend Design

### Pages — `features/roomTypes/` + `pages/`

| File | Responsibility |
|---|---|
| `types/roomType.ts` | `RoomType` (includes `hotelId`), `RoomTypeRequest` |
| `features/roomTypes/roomTypeService.ts` | `listRoomTypes(hotelId)`, `getRoomType(id)`, `createRoomType(hotelId, request)`, `updateRoomType(id, request)`, `deleteRoomType(id)` |
| `pages/HotelRoomTypesPage.tsx` | table of a hotel's room types (Name/Description/Capacity/DailyRate), same pattern as `HotelsPage` (Admin-only write controls, `window.confirm` on delete) |
| `pages/RoomTypeFormPage.tsx` | single create/edit form, same pattern as `HotelFormPage` |

### Routing

```
/hotels/:hotelId/room-types           → ProtectedRoute (no roles)        → HotelRoomTypesPage
/hotels/:hotelId/room-types/new        → ProtectedRoute roles=["Admin"]  → RoomTypeFormPage
/hotels/:hotelId/room-types/:id/edit   → ProtectedRoute roles=["Admin"]  → RoomTypeFormPage
```

`HotelsPage` gains a "Room Types" link per row (visible to any authenticated user). `HotelsPage`'s delete mutation gains an `onError` handler surfacing the backend's `409` message when a hotel still has room types. `HotelRoomTypesPage`'s delete mutation does NOT get the same `onError` treatment in this phase — `RoomType` deletion has no dependent-entity conflict yet (nothing references it), so its only failure mode is a generic error with no specific message to surface. This becomes relevant again in Phase 5, when `Room` will reference `RoomType` and the same Restrict-on-delete pattern will likely repeat — revisit then, not now.

## Out of Scope (explicitly deferred, not silently dropped)

- Rooms, Guests, Reservations, Dashboard — later phases.
- Any UI for viewing/managing room types across all hotels at once (everything is scoped to a single hotel's page).
- Re-parenting a room type to a different hotel (not requested; `HotelId` is set once at creation).
