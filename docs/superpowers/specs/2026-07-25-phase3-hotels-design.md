# Phase 3 — Hotels Design

## Context

Phase 2 delivered Authentication: login, JWT issuance/validation, and the `Admin`/`Receptionist` roles (with `[Authorize(Roles=...)]` established as a mechanism but not yet exercised by any real endpoint — `/api/auth/me` only proved the claim round-trips). Phase 2's design explicitly deferred the frontend `ProtectedRoute` guard "to Phase 3, when the first admin-only page exists." Phase 3, per the MVP Development Order in `prompts/project-01.md`, is Hotels: Create, Update, Delete, List. This is the first real business entity and the first phase where role-based authorization is actually enforced end-to-end (backend `[Authorize(Roles="Admin")]` on writes, frontend `ProtectedRoute` gating admin-only pages).

Unlike Phase 2 (which had a genuine architecture choice — custom JWT vs. full ASP.NET Core Identity), Hotels CRUD has no comparable fork: it follows the vertical-slice pattern already established by `Features/Auth/` and `Features/Health/` (entity + Fluent API config, DTOs with manual mapping, a service holding logic, a thin controller). This document records the decisions the spec leaves open, not an architecture debate.

## Decisions

- **Hotel fields:** `Name`, `Address`, `City`, `Phone` — all required. No fields beyond what's needed to identify and list a hotel; nothing in the spec calls for more.
- **No unique constraint on `Name`:** a hotel chain could legitimately have multiple properties with overlapping naming conventions; nothing in the spec requires uniqueness, and enforcing it would be an invented constraint.
- **Delete is a hard delete.** No entity references `Hotel` yet (Room Types/Rooms arrive in Phases 4-5), so there's no orphan/cascade concern to design around now. Revisit cascade/restrict policy when Rooms exists, not preemptively.
- **Authorization split by verb, matching Phase 2's role design** ("Admin manages hotels/room types/rooms; Receptionist operates reservations/guests day-to-day"): `GET` endpoints (list, get-by-id) require only authentication (`[Authorize]`, either role); `POST`/`PUT`/`DELETE` require `[Authorize(Roles="Admin")]`. Receptionist needs to see hotels (for later reservation flows) but never to create/modify/delete one.
- **`GET /api/hotels/{id}` is added even though the spec only lists Create/Update/Delete/List.** It's required to support "Update Hotel" working at all — the edit page needs to load current values before editing. Not scope creep; a precondition for a listed capability.
- **No pagination on `List Hotels`.** A simple full list is proportionate for an MVP portfolio project; pagination is not requested and would be premature.
- **Single shared `HotelRequest` DTO for both Create and Update.** The request body shape (`Name`, `Address`, `City`, `Phone`) is identical for both operations — two DTOs with identical fields would be duplication for its own sake. Manual mapping still happens explicitly in `HotelService` (no AutoMapper either way).
- **`ProtectedRoute` is built now**, as Phase 2 planned. It accepts an optional `roles` prop: no `roles` means "must be logged in"; `roles={["Admin"]}` means "must be logged in AND have that role" (redirects to `/` if authenticated but wrong role, to `/login` if not authenticated at all).
- **Frontend route-level split mirrors the backend's verb split:** `/hotels` (list) is behind `ProtectedRoute` with no role restriction; `/hotels/new` and `/hotels/:id/edit` are behind `ProtectedRoute roles={["Admin"]}`. Within the list page itself, write actions (New/Edit/Delete buttons) are conditionally rendered only for `Admin` users, so a Receptionist sees a read-only list.
- **Delete confirmation is a plain `window.confirm`.** No custom modal component — proportionate for MVP scope; a polish item for later, not a blocker now.
- **Testing the Receptionist-forbidden path:** since only an `Admin` account is seeded (Phase 2), the integration tests insert a `Receptionist` user directly into the isolated test database (not into the production migration seed — that stays out of scope, matching Phase 2's decision to seed only one account) purely to obtain a real Receptionist JWT via the actual login endpoint and prove `403` is enforced for real, not just asserted in isolation.

## Backend Design

### Data model

`Features/Hotels/Hotel.cs`:

| Field | Type | Rule |
|---|---|---|
| `Id` | `int` (identity) | PK |
| `Name` | `string` | required, max length 200 |
| `Address` | `string` | required, max length 300 |
| `City` | `string` | required, max length 100 |
| `Phone` | `string` | required, max length 20 |

`HotelConfiguration : IEntityTypeConfiguration<Hotel>` sets the above via Fluent API. No seed data (unlike `User`) — hotels are created through the app, not pre-populated.

### Feature slice — `Features/Hotels/`

| File | Responsibility |
|---|---|
| `Hotel.cs`, `HotelConfiguration.cs` | entity, Fluent API |
| `HotelRequest.cs` | shared Create/Update request DTO |
| `HotelResponse.cs` | response DTO |
| `IHotelService.cs` / `HotelService.cs` | `CreateAsync`, `UpdateAsync` (→ `null` if not found), `DeleteAsync` (→ `bool`), `GetByIdAsync` (→ `null` if not found), `ListAsync` — manual entity↔DTO mapping |
| `HotelsController.cs` | thin, delegates only |

### Endpoints

| Route | Verb | Authorization | Not-found behavior |
|---|---|---|---|
| `/api/hotels` | GET | `[Authorize]` | — |
| `/api/hotels/{id}` | GET | `[Authorize]` | `404` |
| `/api/hotels` | POST | `[Authorize(Roles="Admin")]` | — |
| `/api/hotels/{id}` | PUT | `[Authorize(Roles="Admin")]` | `404` |
| `/api/hotels/{id}` | DELETE | `[Authorize(Roles="Admin")]` | `404` |

### Wiring

- New migration `AddHotels` (creates the `Hotels` table; no seed).
- `IHotelService` registered (Scoped) in the existing `Extensions/ApplicationServicesCollectionExtensions.cs` alongside `IHealthService`/`IAuthService`.
- No `Program.cs` changes needed — `[Authorize]`/`[Authorize(Roles=...)]` work with the JWT bearer pipeline Phase 2 already wired up.

### Testing

- `HotelServiceTests` (unit, SQLite-backed like `AuthServiceTests`): create persists and returns the correct response; update modifies an existing row or returns `null` for an unknown id; delete removes a row or returns `false` for an unknown id; list returns everything created.
- `HotelsEndpointsTests` (integration, `WebApplicationFactory<Program>` like `AuthEndpointsTests`): no token → `401` on `GET`; Receptionist token → `200` on `GET`, `403` on `POST`/`PUT`/`DELETE`; Admin token → `200`/`201` on all. The Receptionist test user is inserted directly into the isolated test database in test setup, then logs in for real via `/api/auth/login` to obtain a genuine token — proving enforcement through the real pipeline, not a mocked claim.

## Frontend Design

### `ProtectedRoute`

```
interface ProtectedRouteProps {
  children: ReactNode;
  roles?: string[]; // omitted = "must be logged in"; present = "must also have one of these roles"
}
```

Loading → shows a brief loading state; no `user` → redirect to `/login`; `roles` specified and user's role not included → redirect to `/`; otherwise renders `children`.

### Pages — `features/hotels/` + `pages/`

| File | Responsibility |
|---|---|
| `types/hotel.ts` | `Hotel`, `HotelRequest` |
| `features/hotels/hotelService.ts` | `listHotels`, `getHotel(id)`, `createHotel`, `updateHotel(id)`, `deleteHotel(id)` |
| `pages/HotelsPage.tsx` | table (Name/Address/City/Phone) via `useQuery`; Edit/Delete buttons and "New Hotel" link rendered only when `user.role === "Admin"`; delete confirms via `window.confirm`, then calls the delete mutation, which invalidates the `["hotels"]` query on success so the table refreshes without a manual reload |
| `pages/HotelFormPage.tsx` | single form (React Hook Form + Zod) reused for create and edit; if the route has an `:id`, loads the hotel via `useQuery` and pre-fills; the Zod schema's max-length rules mirror the backend's `HotelConfiguration` exactly (Name 200, Address 300, City 100, Phone 20) so invalid input is caught client-side before a round trip, not only via a `400` from the API; `useMutation` invalidates the `["hotels"]` query on success and navigates back to `/hotels` |

### Routing

```
/hotels           → ProtectedRoute (no roles)        → HotelsPage
/hotels/new        → ProtectedRoute roles=["Admin"]   → HotelFormPage
/hotels/:id/edit   → ProtectedRoute roles=["Admin"]   → HotelFormPage
```

`HomePage` gains a "Hotels" link, visible whenever a user is authenticated (regardless of role — the list itself is readable by both).

## Out of Scope (explicitly deferred, not silently dropped)

- Pagination/search/filtering on the hotels list.
- Soft delete / cascade behavior for entities that will reference `Hotel` later (Room Types, Rooms) — revisit when those phases arrive.
- A polished delete-confirmation modal (currently `window.confirm`).
- Seeding a Receptionist account into the real migration (test-only, per Decisions above).
- Room Types, Rooms, Guests, Reservations, Dashboard — later phases.
