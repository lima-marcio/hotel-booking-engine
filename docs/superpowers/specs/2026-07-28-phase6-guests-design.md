# Phase 6 — Guests Design

## Context

Phases 3-5 delivered Hotels, Room Types, and Rooms — three entities in a single parent/child chain, all writes gated to `Admin`. Phase 6, per the MVP Development Order in `prompts/project-01.md`, is Guests: Register Guest, Update Guest, Search Guest. Unlike the prior three phases, the MVP scope text does not list a Delete capability for Guests, and Guest is a standalone entity with no foreign keys yet — it becomes the parent side of a relationship only in Phase 7 (Reservations), which is out of scope here. The Phase 2 authentication design doc already flagged that the `Guest` entity "will carry email starting Phase 6," so the field exists in the codebase's intent before this document, not invented here.

This document also revisits the role-authorization split first stated in the Phase 3 design doc ("Admin manages hotels/room types/rooms; Receptionist operates reservations/guests day-to-day") and applies it for the first time: Guests is the first feature where write access is not Admin-only.

## Decisions

- **`Guest` fields: `FullName`, `Email`, `Phone`, `DocumentNumber`** — all required. `DocumentNumber` (national ID/passport number) is included because it is standard for hotel guest registration and gives "Search Guest" a second realistic axis beyond name (front-desk staff commonly look a guest up by ID document). No fields beyond what identifying and contacting a guest requires.
- **No unique constraint on any field**, matching the precedent set by `Hotel.Name` in Phase 3: nothing in the spec requires uniqueness, and a guest's email or document number being corrected after a typo is a realistic scenario a unique constraint would fight rather than help. Enforcing uniqueness here would be an invented constraint.
- **No delete.** The MVP scope text lists "Register Guest, Update Guest, Search Guest" only — a deliberate deviation from Hotels/Room Types/Rooms, which all listed Delete explicitly. Taken literally: no `DELETE /api/guests/{id}`, no delete button, no `GuestDeleteResult`-style plumbing. If a future phase needs it, it can be added then.
- **Search and List are the same operation.** `IGuestService.SearchAsync(string? query, ...)` returns every guest when `query` is null/empty/whitespace, and filters by substring match against `FullName`, `Email`, `Phone`, and `DocumentNumber` (case-insensitive) when a query is supplied. A separate `ListAsync` would just be `SearchAsync(null)` under a different name — one method covers both capabilities the spec names ("Search Guest" is the only read capability listed; a plain list is `SearchAsync` called with an empty box).
- **Case-insensitive matching follows the pattern `AuthService.LoginAsync` already established** for username lookup: normalize the query to lowercase in C# once (`query.ToLowerInvariant()`), then compare against each field's `.ToLower()` in the LINQ predicate, rather than introducing a new pattern (e.g. `EF.Functions.Like`) for the first time in this codebase.
- **Authorization has no role restriction — `[Authorize]` only, on every endpoint, including writes.** This is the first feature slice where `POST`/`PUT` are not `[Authorize(Roles="Admin")]`. It directly implements the split already recorded in the Phase 3 design doc: Receptionists register and update guests as part of day-to-day front-desk work, and since `Role` only has two values (`Admin`, `Receptionist`), "both roles can write" is equivalent to no role check at all. `GET` was already open to any authenticated role in every prior phase; this phase makes `POST`/`PUT` open too.
- **`GET /api/guests/{id}` is added even though the MVP text only says Register/Update/Search**, for the same reason Phase 3 added `GET /api/hotels/{id}`: the edit page needs to load current values before editing. A precondition for "Update Guest," not scope creep.
- **Single shared `GuestRequest` DTO for both Create and Update**, matching every prior phase's pattern (`HotelRequest`, `RoomTypeRequest`, `RoomRequest`). Manual mapping in `GuestService`, no AutoMapper.
- **Frontend routes are top-level, not nested** — `/guests`, `/guests/new`, `/guests/:id/edit` — because `Guest` has no parent entity, unlike Room Types (under a hotel) or Rooms (under a room type). All three sit behind `ProtectedRoute` with no `roles` prop, mirroring the backend's lack of role restriction; every authenticated user sees the same write controls (no conditional `isAdmin` rendering the way `HotelsPage` has).
- **`GuestsPage` combines the search box and the list in one page**, one `useQuery` keyed on the current query string (`["guests", query]`). The search input is a plain controlled `<input>` whose value feeds directly into the query key — typing re-fetches on every change, with no submit button and no debounce, matching the project's existing minimalist-MVP bar (no pagination, no live-search infrastructure). No separate search results page or route.
- **The `Sidebar` gains a "Guests" nav item** (icon `Users` from the already-installed `lucide-react`), highlighted when the current route starts with `/guests` — the first time a second registry uses the extensibility point the sidebar's design spec called out ("Additional nav items ... added when/if those registries get their own context-free list pages").

## Backend Design

### Data model

`Features/Guests/Guest.cs`:

| Field | Type | Rule |
|---|---|---|
| `Id` | `int` (identity) | PK |
| `FullName` | `string` | required, max length 200 |
| `Email` | `string` | required, max length 254 |
| `Phone` | `string` | required, max length 20 |
| `DocumentNumber` | `string` | required, max length 30 |

`GuestConfiguration : IEntityTypeConfiguration<Guest>` sets the above via Fluent API — no relationships, no unique indexes.

### Feature slice — `Features/Guests/`

| File | Responsibility |
|---|---|
| `Guest.cs`, `GuestConfiguration.cs` | entity, Fluent API |
| `GuestRequest.cs` | shared Create/Update DTO (`FullName`, `Email`, `Phone`, `DocumentNumber`) |
| `GuestResponse.cs` | response DTO (same four fields plus `Id`) |
| `IGuestService.cs` / `GuestService.cs` | `SearchAsync(query)` → `List<GuestResponse>`; `CreateAsync(request)` → `GuestResponse`; `UpdateAsync(id, request)` → `GuestResponse?` (`null` if not found); `GetByIdAsync(id)` → `GuestResponse?` |
| `GuestsController.cs` | one controller, thin, delegates only |

### Endpoints

| Route | Verb | Authorization | Notes |
|---|---|---|---|
| `/api/guests` | GET | `[Authorize]` | Optional `?query=` filters by name/email/phone/document; omitted or blank returns all guests |
| `/api/guests/{id}` | GET | `[Authorize]` | `404` if not found |
| `/api/guests` | POST | `[Authorize]` | `201` with the created guest |
| `/api/guests/{id}` | PUT | `[Authorize]` | `200` with the updated guest; `404` if not found |

No `DELETE` route in this phase.

### Testing

- `GuestServiceTests` (unit, SQLite-backed, same pattern as `HotelServiceTests`/`RoomServiceTests`): create persists and returns the correct response; update existing id succeeds and persists changes; update unknown id returns `null`; get-by-id existing/unknown; search with no query returns all guests; search with a query matching `FullName` returns only matches; a separate case proving the match works against `Email`, `Phone`, and `DocumentNumber` too; search is case-insensitive (query in a different case than the stored value still matches).
- `GuestsEndpointsTests` (integration, `WebApplicationFactory<Program>`, same pattern as `RoomsEndpointsTests`): all endpoints return `401` without a token; a `Receptionist` account (inserted directly into the test database, same technique Phase 3 established) succeeds on `POST`/`PUT`/`GET` — proving the no-role-restriction decision holds for real, not just by omission; `GET /api/guests/{id}` with an unknown id returns `404`; `PUT` with an unknown id returns `404`; create→search round trip proves a newly created guest is findable by a partial, differently-cased query against each of the four fields.

## Frontend Design

### Pages — `features/guests/` + `pages/`

| File | Responsibility |
|---|---|
| `types/guest.ts` | `Guest`, `GuestRequest` |
| `features/guests/guestService.ts` | `searchGuests(query?)`, `getGuest(id)`, `createGuest(request)`, `updateGuest(id, request)` |
| `pages/GuestsPage.tsx` | search box + table (FullName/Email/Phone/DocumentNumber), "New Guest" link, "Edit" per row, no delete |
| `pages/GuestFormPage.tsx` | single create/edit form, same pattern as `HotelFormPage` |

### Routing

```
/guests           → ProtectedRoute (no roles) → GuestsPage
/guests/new        → ProtectedRoute (no roles) → GuestFormPage
/guests/:id/edit   → ProtectedRoute (no roles) → GuestFormPage
```

`Sidebar` gains a second nav item, "Guests" (`Users` icon), positioned after "Hotels," highlighted via `location.pathname.startsWith("/guests")` — the same mechanism already used for "Hotels."

### Zod validation

Mirrors the backend: `fullName` required max 200, `email` required max 254 and a valid email format (`.email()` — the first email field in the app; validated at the form layer only, matching the backend's plain required+maxlength check), `phone` required max 20, `documentNumber` required max 30.

## Out of Scope (explicitly deferred, not silently dropped)

- Reservations, Dashboard — later phases.
- Delete Guest — not in the MVP scope text for this phase; revisit only if a later phase's spec calls for it.
- Any uniqueness constraint on Email/DocumentNumber.
- Linking a Guest to any Reservation — that FK is created in Phase 7, on the `Reservation` side, not here.
- Pagination on search results — a simple full/filtered list is proportionate for an MVP portfolio project, same reasoning as every prior phase's list endpoint.
- Debounced/live search-as-you-type — the search box re-queries on demand (page load and query change trigger the `useQuery`), no explicit debounce mechanism is introduced.
