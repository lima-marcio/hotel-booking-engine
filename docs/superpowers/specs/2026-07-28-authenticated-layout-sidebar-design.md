# Authenticated Layout — Collapsible Sidebar Design

## Context

Every page in the frontend today is a standalone `<main className="min-h-screen ...">` with no shared chrome. The only navigation available to a logged-in user is a single "Hotels" link and a "Log out" button, both hardcoded into `HomePage`; every other page (`HotelsPage`, `HotelRoomTypesPage`, `RoomTypeRoomsPage`, all form pages) has no way back to the registries list except the browser back button or in-page back-links. As more registries are added in future phases (Guests, Reservations, ...), this gap gets worse. This document adds a persistent, collapsible left sidebar for authenticated users, wrapping every page instead of being bolted onto one.

## Decisions

- **A single `AppLayout` component wraps the entire route tree, not each page individually.** `AppRoutes.tsx` wraps its `<Routes>` in `<AppLayout>`. `AppLayout` reads `useAuth()`: while `isLoading` or when there is no `user`, it renders `{children}` with no chrome at all (this covers `LoginPage` and `HomePage` for anonymous visitors, and avoids a layout flash during the initial session check). When a `user` is present, it renders the sidebar alongside the page content. This means the sidebar appears on every page an authenticated user sees, including `HomePage` — no per-page changes needed, and no dependency on which pages happen to be wrapped in `ProtectedRoute`.
- **The sidebar is a new `Sidebar` component, not folded into `AppLayout`.** `AppLayout` is pure layout/auth-gating; `Sidebar` owns navigation items, collapse state, and the user/logout block. Keeping them separate means `Sidebar` can be understood and modified without touching the auth-gating logic.
- **Collapse state is local `Sidebar` state, persisted to `localStorage` under the key `sidebar-collapsed`.** Initialized via `useState(() => localStorage.getItem("sidebar-collapsed") === "true")`, written back in a `useEffect` on every change. No new context/store — only one component reads or writes this state.
- **Navigation items today: only "Hotels".** Room Types and Rooms remain drill-down-only (reached from a specific hotel/room-type row), matching how the backend and existing pages already scope them — the sidebar does not invent a context-free entry point for either. Future phases add their own top-level entries here as new registries gain list pages of their own.
- **The user/logout block that currently lives in `HomePage` moves into the sidebar's footer.** Because `AppLayout` now shows the sidebar on `HomePage` too when logged in, that block becomes redundant there and is removed from `HomePage`; `HomePage` keeps only the title, the API health check, and a "Login" link for anonymous visitors (`!isAuthLoading && !user`).
- **Icons come from `lucide-react`**, a new frontend dependency. This is a deliberate exception to "no new packages" from prior phases — this project's `Global Constraints` bar on new packages targets each *feature* phase's own scope; this is a cross-cutting layout change and the user explicitly chose an icon library over a text/initials-only fallback.
- **Collapsed and expanded are the same DOM structure, styled differently** (`w-56` vs `w-14`), not two different component trees — every element (toggle, nav link, footer) stays mounted and just swaps between icon-only and icon+label rendering, so there's a single source of truth for what the sidebar contains.

## Frontend Design

### Components

| File | Responsibility |
|---|---|
| `components/AppLayout.tsx` | Auth-gated chrome wrapper. No sidebar when logged out/loading; `flex min-h-screen` with `<Sidebar />` + content when logged in. |
| `components/Sidebar.tsx` | Collapsible nav: toggle button, "Hotels" nav item, user/logout footer. Owns collapse state and its `localStorage` persistence. |

### `AppLayout`

```tsx
export function AppLayout({ children }: { children: ReactNode }) {
  const { user, isLoading } = useAuth();

  if (isLoading || !user) {
    return <>{children}</>;
  }

  return (
    <div className="flex min-h-screen">
      <Sidebar />
      <div className="flex-1">{children}</div>
    </div>
  );
}
```

Wired in `AppRoutes.tsx` by wrapping the existing `<Routes>...</Routes>` — no other route wiring changes.

### `Sidebar`

- Root: `<aside>` with width `w-56` expanded / `w-14` collapsed (`transition-all`), full height, right border.
- Top: app title (hidden when collapsed) + toggle button. Icon: `PanelLeftClose` when expanded, `PanelLeftOpen` when collapsed.
- Nav: single item, "Hotels", icon `Building2`. `useLocation()` highlights it (background/text color change) when `location.pathname.startsWith("/hotels")`. Expanded shows icon + "Hotels" label; collapsed shows only the icon with `title="Hotels"` for a native tooltip.
- Footer (pinned to bottom via `mt-auto`): `UserRound` icon + `{user.username} ({user.role})` when expanded, icon only (with `title` showing the same text) when collapsed; below it, a logout button (`LogOut` icon + "Logout" label expanded, icon-only with `title="Logout"` collapsed) calling `useAuth().logout()` then navigating to `/login`.

### `HomePage` changes

Remove the `user ? (...) : (<Link to="/login">...)` block entirely except the anonymous branch:

```tsx
{!isAuthLoading && !user && (
  <Link to="/login" className="text-blue-600 underline">
    Login
  </Link>
)}
```

The API health check output is unchanged.

### Dependency

`npm install lucide-react` in `frontend/`. Icons used: `PanelLeftClose`, `PanelLeftOpen`, `Building2`, `UserRound`, `LogOut` — confirmed present in the installed package before use.

## Testing

No frontend automated test framework exists in this project (confirmed — no test runner configured, no existing `*.test.*`/`*.spec.*` files). Verification follows the same pattern every prior phase has used:

- `npm run build` — 0 TypeScript errors.
- Manual browser walkthrough: log in, confirm the sidebar appears on Home and every registry page; toggle collapse and confirm it persists across a page reload; confirm the "Hotels" item highlights on `/hotels` and its sub-routes; confirm logout from the sidebar works from at least two different pages (e.g., `HomePage` and `HotelsPage`); confirm the sidebar is entirely absent on `/login` and on `/` while logged out.

## Out of Scope (explicitly deferred, not silently dropped)

- Additional nav items for Room Types/Rooms or any other registry — added when/if those registries get their own context-free list pages.
- Mobile-specific responsive behavior (e.g., an overlay/drawer sidebar on small screens) — not requested; the collapse feature already gives back most of the width on narrow viewports.
- Any role-based hiding of the "Hotels" nav item — reads are open to any authenticated role today, same as the existing `/hotels` route.
- Keyboard shortcuts for toggling the sidebar.
