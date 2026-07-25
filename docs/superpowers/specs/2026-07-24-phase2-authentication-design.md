# Phase 2 — Authentication Design

## Context

Phase 1 delivered the solution scaffold (backend + frontend running, wired together via a trivial `Features/Health` slice; `AppDbContext` intentionally empty). Phase 2, per the MVP Development Order in `prompts/project-01.md`, is Authentication: Login, JWT, Role Authorization. The spec names these three capabilities but leaves several things undefined — which roles exist, how staff accounts get created (no "Register" endpoint is listed), and whether this phase includes the frontend login flow or backend only. This document resolves those gaps and locks down the design before implementation planning starts.

## Decisions

- **Roles:** `Admin` and `Receptionist`. Modeled as a C# enum on `User`, not a separate roles table — only two fixed roles, so a normalized role schema (à la ASP.NET Identity) would be unused complexity.
- **Account provisioning:** no self-registration. A single `Admin` user is seeded via EF Core migration `HasData`. Creating additional users (e.g. an Admin-only "create Receptionist" endpoint) is explicitly deferred, not part of this phase.
- **Scope:** backend (login + JWT issuance/validation + role claims) **and** frontend (login page, token storage, Home reflects auth state) — the spec's Final Objective lists "Authenticate" as a user-facing action, so Phase 2 should be demonstrable end-to-end in a browser, not backend-only.
- **Demonstrating role authorization:** a `GET /api/auth/me` endpoint, `[Authorize]`-protected, returning the caller's id/username/role from JWT claims. No other business endpoint exists yet to protect (Hotels/Rooms arrive in Phase 3), so this is the vehicle for proving the auth pipeline actually works, not a business feature in its own right.
- **Login credential:** `Username`, not email — avoids conflating staff accounts with the `Guest` entity (which will carry email starting Phase 6).
- **No `ProtectedRoute` frontend guard yet:** there is no admin-only page to gate in this phase (that starts in Phase 3 with Hotels management). Building a route guard with no consumer would be unused infrastructure. It will be added in Phase 3 when the first admin-only page exists. This phase proves the auth flow via the Home page displaying "logged in as X (role)" using data fetched from the protected `/me` endpoint.
- **No refresh tokens:** out of scope for this phase. Access token expires (60 minutes); re-login is the only path back. Documented here as a deliberate omission, not a Future Feature to formally track — small enough to reconsider later without ceremony.
- **Auth implementation approach:** custom JWT issuance + `PasswordHasher<User>` (from `Microsoft.Extensions.Identity.Core`, used standalone — not the full `Microsoft.AspNetCore.Identity.EntityFrameworkCore` stack). Rejected full ASP.NET Core Identity: it brings a normalized multi-table user/role/claims schema that's unused complexity for two fixed roles and no self-registration, and still requires a custom JWT layer on top since Identity doesn't issue JWTs itself. The lighter approach matches the project's established conventions (manual mapping, no AutoMapper, thin feature-vertical slices like `Features/Health`).
- **Frontend token storage:** a small dedicated `stores/tokenStore.ts` (get/set/clear, backed by `localStorage`) is the single source of truth for the token, consumed both by an Axios request interceptor (auto-attaches `Authorization: Bearer <token>`) and by `AuthContext` (which layers user state + login/logout on top). No state-management library is added — React Context is sufficient and keeps the approved frontend stack unchanged.
- **Frontend testing:** none added. Vitest/RTL are not part of the frontend stack defined in `.ai/20-frontend.md` and were not introduced in Phase 1 either. Verification stays manual (build + browser walkthrough), same as Phase 1's closing verification.

## Backend Design

### Data model

`Features/Auth/User.cs` — first real entity in the project:

| Field | Type | Rule |
|---|---|---|
| `Id` | `int` (identity) | PK |
| `Username` | `string` | required, unique index |
| `PasswordHash` | `string` | produced by `PasswordHasher<User>` |
| `Role` | `Role` enum (`Admin`, `Receptionist`) | stored as string via Fluent API conversion |

`Features/Auth/UserConfiguration.cs` implements `IEntityTypeConfiguration<User>`: unique index on `Username`, required fields, enum-to-string conversion, and a `HasData` seed for one `admin` user (`Role = Admin`). The seed password is a fixed, pre-computed `PasswordHasher` output for a documented development password (documented in the implementation plan and README) — `HasData` requires a static value, so the hash is computed once at implementation time, not generated at migration-run time.

**Username comparison:** SQLite (dev) compares `TEXT` case-sensitively by default; SQL Server (prod) uses a case-insensitive collation by default — looking up `Username` with a plain `==` would behave differently between environments. `AuthService.Login` normalizes both sides explicitly (e.g. `.ToLowerInvariant()` on the incoming username before querying, matching against a `Username` column populated in a consistent case), so lookup behavior is identical in both environments regardless of provider collation.

### Feature slice — `Features/Auth/`

| File | Responsibility |
|---|---|
| `User.cs`, `Role.cs`, `UserConfiguration.cs` | entity, enum, Fluent API + seed |
| `LoginRequest.cs`, `LoginResponse.cs`, `CurrentUserResponse.cs` | DTOs |
| `IJwtTokenGenerator.cs` / `JwtTokenGenerator.cs` | builds a signed JWT for a `User` (claims: id, username, role) |
| `JwtOptions.cs` | POCO bound to the `Jwt` config section (`Issuer`, `Audience`, `SigningKey`, `ExpiryMinutes`) |
| `IAuthService.cs` / `AuthService.cs` | `Login(request)`: looks up user, verifies password via `PasswordHasher<User>`, returns a token via `IJwtTokenGenerator`, or `null` on failure. `GetCurrentUser(ClaimsPrincipal)`: reads id/username/role from claims. |
| `AuthController.cs` | `POST /api/auth/login` (`[AllowAnonymous]`) → `401` on failure, `200` + `LoginResponse` on success. `GET /api/auth/me` (`[Authorize]`) → `200` + `CurrentUserResponse`. Delegates only — no logic in the controller itself. |

### Wiring

- New `Extensions/JwtAuthenticationServiceCollectionExtensions.cs` → `AddJwtAuthentication(IServiceCollection, IConfiguration)`: binds `JwtOptions`, calls `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` with `TokenValidationParameters` built from those options (validate issuer, audience, lifetime, signing key).
- `appsettings.json` / `appsettings.Development.json` gain a `Jwt` section. The signing key is a development-only placeholder (≥32 chars, matching the existing pattern for the SQL Server connection string — production needs a real secret supplied out-of-band, not enforced in code beyond documentation).
- `Program.cs`: add `builder.Services.AddJwtAuthentication(builder.Configuration);` alongside the other `AddX` calls. The only pipeline change is inserting `app.UseAuthentication();` immediately before the existing `app.UseAuthorization();` — CORS → Authentication → Authorization → MapControllers. Nothing else in `Program.cs` reorders.
- New EF Core migration `AddUsers` (creates the `Users` table + the seeded admin row).
- `Extensions/ApplicationServicesCollectionExtensions.cs` (existing file) gains registrations for `IAuthService → AuthService` and `IJwtTokenGenerator → JwtTokenGenerator`, both Scoped.

### Testing

- `JwtTokenGeneratorTests` — generated token carries the expected claims (id, username, role) and expiry consistent with configured `ExpiryMinutes`.
- `AuthServiceTests` — correct credentials return a token; wrong password or unknown username return `null`. Uses a test `AppDbContext` backed by SQLite (not EF's `InMemory` provider — keeps tests on the same provider family the app actually uses).
- `AuthEndpointsTests` — integration test via `WebApplicationFactory<Program>` (the `public partial class Program {}` from Phase 1 exists for exactly this) against an isolated test SQLite database with migrations applied (so the seed exists). Exercises the real flow: bad login → `401`; good login → `200` + token; `/me` without a token → `401`; `/me` with the token from login → `200` with the correct identity.

## Frontend Design

| File | Responsibility |
|---|---|
| `types/auth.ts` | `LoginRequest`, `LoginResponse`, `CurrentUser` |
| `features/auth/authService.ts` | `login()` → `POST /api/auth/login`; `fetchCurrentUser()` → `GET /api/auth/me` |
| `stores/tokenStore.ts` | single source of truth for the JWT (get/set/clear), persisted to `localStorage` |
| `stores/AuthContext.tsx` | Context provider exposing `{ user, login(), logout() }`; on mount, if a token is already stored, validates it against `/me` (clears it if invalid) |
| `hooks/useAuth.ts` | hook wrapping `useContext(AuthContext)` |
| `pages/LoginPage.tsx` | React Hook Form + Zod validated form; calls `login()`; navigates to `/` on success |

`api/httpClient.ts` gains a request interceptor that reads `tokenStore` and attaches `Authorization: Bearer <token>` when present — without this, `/me` could never be called successfully from the frontend.

Routing: adds `/login`. The existing `HomePage` is extended (not replaced) to also show, alongside the API health status: "Logged in as {username} ({role})" plus a Logout button when authenticated, or a link to `/login` when not. This proves the full loop (login → token stored → `/me` call succeeds) without introducing a page outside this phase's scope.

## Out of Scope (explicitly deferred, not silently dropped)

- Admin-only user creation endpoint.
- `ProtectedRoute` frontend route guard (arrives in Phase 3 with the first admin-only page).
- Refresh tokens.
- Frontend automated tests (not in the approved stack).
- Password reset / account recovery.

## Known Limitation (consciously accepted)

The seeded `admin`/`Admin123!` account (see Data Model) is created by the `AddUsers` migration regardless of environment, and the password is documented in the root `README.md`. Flagged in Phase 2's final review as a real production-hardening gap — a real deployment would ship with a publicly-known admin password and no built-in rotation path. Explicitly accepted for this project: it is a portfolio piece that is not deployed to a real production environment, and the simplicity of "one seeded account, no self-registration" is worth more here than production-grade credential hygiene. If this project is ever deployed for real, gate the `HasData` seed to non-Production (or replace it with an out-of-band admin bootstrap step) before doing so.
