# Phase 5 — Rooms Design

## Context

Phase 4 delivered Room Types CRUD, the first parent/child relationship (`RoomType` belongs to a `Hotel`) and the first Restrict-on-delete rule. Phase 5, per the MVP Development Order in `prompts/project-01.md`, is Rooms: Room Number, Room Type, Capacity, Status, Daily Rate. As already flagged in the Phase 4 design doc, `Capacity` and `DailyRate` moved to `RoomType` in that phase — a real physical room inherits both from its room type rather than duplicating them. Phase 5 confirms that deviation and adds a second one: `Room` gets a denormalized `HotelId` in addition to `RoomTypeId`, rather than resolving the hotel only through a join.

This document records the relationship, delete-safety, uniqueness, and routing decisions for Rooms.

## Decisions

- **`Room` belongs to a `RoomType` via a required `RoomTypeId` foreign key, and also carries a required `HotelId` foreign key directly.** `HotelId` is not independently settable — it is copied from `RoomType.HotelId` by `RoomService` at creation time and never changes afterward (there is no UI to re-parent a room to a different room type, so the two FKs can never drift apart). The direct `HotelId` exists so hotel-scoped queries (room-number uniqueness, and any future hotel-wide room listing) don't require a join through `RoomType`.
- **`RoomNumber` must be unique within a `Hotel`** (not within a `RoomType` — two room types in the same hotel cannot reuse a room number; two different hotels can both have a "101"). Enforced in `RoomService` at the application layer (no DB-level unique index, consistent with how this project enforces business rules in services rather than at the schema level beyond required/max-length).
- **`Status` is a `RoomStatus` enum with two values in this phase: `Available`, `Maintenance`.** It reflects manual, staff-set physical usability only — not reservation-driven availability. `Occupied`/booking-derived availability is explicitly deferred to Phase 7 (Reservations), which will check date-range overlap instead of relying on a static status field.
- **`RoomStatus` is bound directly as a typed enum on `RoomRequest`/`RoomResponse`**, decorated with `[JsonConverter(typeof(JsonStringEnumConverter))]`, rather than exposed as a plain `string` the way `Role` is on `LoginResponse`. This is a deliberate improvement over that earlier pattern: letting ASP.NET Core's model binder parse the enum means an invalid status string in the request body fails with an automatic `400` before any service code runs, instead of requiring a manual parse-and-validate step.
- **Deleting a `RoomType` that still has `Room`s is blocked (Restrict), not cascaded** — the same pattern Phase 4 established for `Hotel` → `RoomType`. `IRoomTypeService.DeleteAsync`'s contract changes from `Task<bool>` to `Task<RoomTypeDeleteResult>` (`Deleted`/`NotFound`/`HasRooms`), and `RoomTypesController.Delete` maps the three outcomes to `204`/`404`/`409`.
- **The `Room` → `Hotel` foreign key also gets `DeleteBehavior.Restrict` at the EF level, but this is defense-in-depth, not a new application-layer check.** Because every `Room` requires a `RoomType`, and `RoomType` deletion is now blocked while any `Room` references it, a `Hotel` can never reach a state of "has Rooms but no RoomTypes" — the existing `HotelService.DeleteAsync` check for `RoomType`s already transitively covers Rooms. No new `HasRooms` case is added to `HotelDeleteResult`; the direct FK's `Restrict` is purely a database-level safety net in case some future code path ever bypasses the service layer.
- **Create and Update need to distinguish two different failure modes**, not just "not found" vs. success: the parent `RoomType` (create) or the `Room` itself (update) doesn't exist, or the `RoomNumber` collides with another room in the same hotel. A plain nullable return can't carry both a discriminant and a payload, so `IRoomService.CreateAsync`/`UpdateAsync` return a small result type:
  ```csharp
  public enum RoomSaveOutcome { Success, ParentNotFound, DuplicateRoomNumber }
  public record RoomSaveResult(RoomSaveOutcome Outcome, RoomResponse? Room);
  ```
  `RoomsController` maps `Success` → `201`/`200` with the room, `ParentNotFound` → `404`, `DuplicateRoomNumber` → `409`.
- **Routing mirrors the parent/child relationship, one level deeper than Phase 4**: `GET`/`POST` live under `/api/room-types/{roomTypeId}/rooms`; `GET`/`PUT`/`DELETE` by id live under the flat `/api/rooms/{id}`. One controller, absolute per-action route templates (no class-level `[Route]`), same reasoning as `RoomTypesController`.
- **`ListByRoomTypeAsync`/`CreateAsync` return `ParentNotFound`/`null` specifically to mean "the room type doesn't exist"** (→ `404`), distinct from an empty list (a real room type with zero rooms yet → `200` with `[]`).
- **Authorization mirrors Hotels/RoomTypes exactly**: `GET` requires only `[Authorize]`; `POST`/`PUT`/`DELETE` require `[Authorize(Roles="Admin")]` in addition.
- **Frontend routes nest under the room type** (`/hotels/:hotelId/room-types/:roomTypeId/rooms`, `/rooms/new`, `/rooms/:id/edit`); `hotelId` stays in the URL purely for the back-link/breadcrumb to the hotel's room types page — the API itself never needs it, since `roomTypeId`/`id` are already sufficient. `HotelRoomTypesPage` gains a "Rooms" link per row, visible to any authenticated user (reads are open).
- **`RoomFormPage`'s create/update mutation needs an `onError` handler** surfacing the backend's new `409` (duplicate room number) — the first time a create/update form (not just a delete) in this project needs to display a business-rule conflict from the API, using the same `isAxiosError` type guard already fixed on the Phase 4 delete-error handling.

## Backend Design

### Data model

`Features/Rooms/Room.cs`:

| Field | Type | Rule |
|---|---|---|
| `Id` | `int` (identity) | PK |
| `RoomTypeId` | `int` | required FK → `RoomType.Id`, `OnDelete(Restrict)` |
| `HotelId` | `int` | required FK → `Hotel.Id`, `OnDelete(Restrict)`; copied from `RoomType.HotelId` at creation, never client-supplied |
| `RoomNumber` | `string` | required, max length 20 |
| `Status` | `RoomStatus` | required |

`Features/Rooms/RoomStatus.cs`:

```csharp
public enum RoomStatus
{
    Available,
    Maintenance
}
```

`RoomConfiguration : IEntityTypeConfiguration<Room>` sets the above, both FK relationships with `DeleteBehavior.Restrict` explicitly configured (same `Microsoft.Data.Sqlite` foreign-key-enforcement reasoning as `RoomTypeConfiguration`), and stores `Status` via `.HasConversion<string>()` so the column is human-readable.

### Feature slice — `Features/Rooms/`

| File | Responsibility |
|---|---|
| `Room.cs`, `RoomStatus.cs`, `RoomConfiguration.cs` | entity, enum, Fluent API |
| `RoomRequest.cs` | shared Create/Update DTO (`RoomNumber`, `Status` — no `RoomTypeId`/`HotelId`, supplied by the route/derived) |
| `RoomResponse.cs` | response DTO (includes `RoomTypeId` and `HotelId`) |
| `RoomSaveResult.cs` | `RoomSaveOutcome` enum + `RoomSaveResult` record, shared by Create/Update |
| `IRoomService.cs` / `RoomService.cs` | `ListByRoomTypeAsync(roomTypeId)` → `null` if the room type doesn't exist; `CreateAsync(roomTypeId, request)` / `UpdateAsync(id, request)` → `RoomSaveResult`; `GetByIdAsync(id)` → `null` if not found; `DeleteAsync(id)` → `bool` |
| `RoomsController.cs` | one controller, absolute per-action routes, thin (delegates only) |

### Endpoints

| Route | Verb | Authorization | Notes |
|---|---|---|---|
| `/api/room-types/{roomTypeId}/rooms` | GET | `[Authorize]` | `404` if room type doesn't exist |
| `/api/room-types/{roomTypeId}/rooms` | POST | `[Authorize(Roles="Admin")]` | `404` if room type doesn't exist; `409` if room number already used in that hotel |
| `/api/rooms/{id}` | GET | `[Authorize]` | `404` if not found |
| `/api/rooms/{id}` | PUT | `[Authorize(Roles="Admin")]` | `404` if not found; `409` if room number already used in that hotel |
| `/api/rooms/{id}` | DELETE | `[Authorize(Roles="Admin")]` | `404` if not found |

### Changes to existing Room Types code (Phase 4)

`IRoomTypeService.DeleteAsync` changes from `Task<bool>` to `Task<RoomTypeDeleteResult>`:

```csharp
public enum RoomTypeDeleteResult { Deleted, NotFound, HasRooms }
```

`RoomTypeService.DeleteAsync` checks for any `Room` referencing the room type before deleting; `RoomTypesController.Delete` maps `Deleted → 204`, `NotFound → 404`, `HasRooms → 409` (with a message body explaining why). No changes are needed to `HotelService`/`HotelsController` — see the Decisions section above for why the existing `HasRoomTypes` check already covers Rooms transitively.

### Testing

- `RoomServiceTests` (unit, SQLite-backed, same pattern as `RoomTypeServiceTests`): create with a valid room type id persists and returns the correct response with `HotelId` copied from the room type; create with an unknown room type id returns `ParentNotFound`; create with a room number already used in the same hotel (even under a different room type) returns `DuplicateRoomNumber`; the same room number in a *different* hotel succeeds; update existing/unknown id, and update that collides with another room's number in the same hotel; delete existing/unknown id; get-by-id existing/unknown id; `ListByRoomTypeAsync` returns only the requested room type's rooms and `null` for an unknown room type id.
- `RoomTypeServiceTests` updates: the two existing delete tests move to the new enum contract; one new test proves a room type with an existing room returns `HasRooms` and is NOT actually deleted.
- `RoomsEndpointsTests` (integration, `WebApplicationFactory<Program>`, same pattern as `RoomTypesEndpointsTests`): list/create with an unknown room type id → `404`; Receptionist → `200` on reads, `403` on writes; Admin → success on all, including the create→list round trip and the duplicate-room-number → `409` case.
- `RoomTypesEndpointsTests` addition: one integration test proving `DELETE /api/room-types/{id}` returns a real `409` when the room type has an existing room.

## Frontend Design

### Pages — `features/rooms/` + `pages/`

| File | Responsibility |
|---|---|
| `types/room.ts` | `Room` (includes `roomTypeId`, `hotelId`), `RoomRequest`, `RoomStatus` union type |
| `features/rooms/roomService.ts` | `listRooms(roomTypeId)`, `getRoom(id)`, `createRoom(roomTypeId, request)`, `updateRoom(id, request)`, `deleteRoom(id)` |
| `pages/RoomTypeRoomsPage.tsx` | table of a room type's rooms (RoomNumber/Status), same pattern as `HotelRoomTypesPage` (Admin-only write controls, `window.confirm` on delete) |
| `pages/RoomFormPage.tsx` | single create/edit form, same pattern as `RoomTypeFormPage`, plus a `409` conflict message on submit |

### Routing

```
/hotels/:hotelId/room-types/:roomTypeId/rooms           → ProtectedRoute (no roles)        → RoomTypeRoomsPage
/hotels/:hotelId/room-types/:roomTypeId/rooms/new        → ProtectedRoute roles=["Admin"]  → RoomFormPage
/hotels/:hotelId/room-types/:roomTypeId/rooms/:id/edit   → ProtectedRoute roles=["Admin"]  → RoomFormPage
```

`HotelRoomTypesPage` gains a "Rooms" link per row (visible to any authenticated user). `RoomTypeRoomsPage` fetches both `getHotel(hotelId)` and `getRoomType(roomTypeId)` to render a header like "Rooms — Deluxe (Grand Hotel)". `RoomFormPage`'s create/update mutation gains an `onError` handler that checks `isAxiosError(err) && err.response?.status === 409` and displays the backend's message inline — the first form-level (not just delete-level) business-rule error surfaced in this project.

## Out of Scope (explicitly deferred, not silently dropped)

- Guests, Reservations, Dashboard — later phases.
- Reservation-driven availability or an `Occupied` status — Phase 7 will check date-range overlap directly; `Status` here stays manual and physical (`Available`/`Maintenance`) only.
- Re-parenting a room to a different room type or hotel (not requested; `RoomTypeId`/`HotelId` are set once at creation).
- Any UI for viewing all of a hotel's rooms without going through a room type, or across all hotels at once.
- Bulk room creation or automatic room-number generation.
