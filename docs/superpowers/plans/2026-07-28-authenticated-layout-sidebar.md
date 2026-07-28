# Authenticated Layout — Collapsible Sidebar Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a persistent, collapsible left sidebar for authenticated users, replacing the single hardcoded "Hotels"/"Log out" block currently stuck on `HomePage` with navigation chrome that wraps every page.

**Architecture:** A new `AppLayout` component wraps the entire route tree (in `AppRoutes.tsx`) and is auth-gated: it renders children bare while logged out/loading, and renders a new `Sidebar` component alongside children once a user is authenticated. `Sidebar` owns its own collapse state (persisted to `localStorage`), a single "Hotels" nav item (the only context-free registry today), and a user-info/logout footer moved out of `HomePage`. No backend changes; no new routes.

**Tech Stack:** React 19, react-router-dom 7, Tailwind CSS 4. New dependency: `lucide-react` (icons) — the user explicitly chose an icon library over a text/initials-only fallback for the collapsed state.

## Global Constraints

- No backend changes — this is a frontend-only, cross-cutting layout change.
- No new frontend dependency other than `lucide-react`.
- Collapse state persists across reloads via `localStorage` key `sidebar-collapsed`.
- Sidebar shows only one nav item today: "Hotels". Room Types/Rooms stay drill-down-only — do not add nav entries for them.
- The user/logout block currently in `HomePage` moves into the sidebar footer and is removed from `HomePage`.
- No frontend automated test framework exists in this project — verification is `npm run build` (0 TypeScript errors) plus a manual browser walkthrough, matching every prior phase.
- `tsconfig.app.json` has `verbatimModuleSyntax: true`, `noUnusedLocals: true`, `noUnusedParameters: true` — type-only imports must use `import type`, and no unused locals/params are allowed.
- One class/component per file, clear names, no abbreviations, no TODOs, no commented dead code. [30-conventions.md]
- Commit messages follow Conventional Commits, no `Co-Authored-By`/session trailers. [30-conventions.md]
- Never hardcode data in the frontend; consume only the backend API. [prompts/project-01.md] (Not implicated here — no data-fetching changes.)

**Design spec:** `docs/superpowers/specs/2026-07-28-authenticated-layout-sidebar-design.md` — read for full rationale; this plan implements it as-is.

---

### Task 1: Install `lucide-react` and build the `Sidebar` component

**Files:**
- Modify: `frontend/package.json`, `frontend/package-lock.json` (via `npm install`)
- Create: `frontend/src/components/Sidebar.tsx`

**Interfaces:**
- Consumes: `useAuth()` (`frontend/src/hooks/useAuth.ts`) → `{ user: CurrentUser | null, logout: () => void }`; `CurrentUser { id, username, role }` (`frontend/src/types/auth.ts`).
- Produces: `Sidebar` component (default-exportless, named export `Sidebar`), no props. Task 2's `AppLayout` renders `<Sidebar />` directly.

- [ ] **Step 1: Install the dependency**

```bash
cd frontend && npm install lucide-react
```

- [ ] **Step 2: Verify the install**

```bash
grep lucide-react frontend/package.json
```

Expected: a line like `"lucide-react": "^0.x.x"` under `dependencies`.

- [ ] **Step 3: Create the `Sidebar` component**

```tsx
import { useEffect, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { Building2, LogOut, PanelLeftClose, PanelLeftOpen, UserRound } from "lucide-react";
import { useAuth } from "../hooks/useAuth";

const COLLAPSED_STORAGE_KEY = "sidebar-collapsed";

export function Sidebar() {
  const { user, logout } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const [collapsed, setCollapsed] = useState(
    () => localStorage.getItem(COLLAPSED_STORAGE_KEY) === "true",
  );

  useEffect(() => {
    localStorage.setItem(COLLAPSED_STORAGE_KEY, String(collapsed));
  }, [collapsed]);

  if (!user) {
    return null;
  }

  const isHotelsActive = location.pathname.startsWith("/hotels");

  function handleLogout() {
    logout();
    navigate("/login");
  }

  return (
    <aside
      className={`flex h-screen flex-col border-r bg-gray-50 transition-all ${collapsed ? "w-14" : "w-56"}`}
    >
      <div className="flex items-center justify-between p-3">
        {!collapsed && <span className="truncate text-sm font-semibold">Hotel Booking Engine</span>}
        <button
          onClick={() => setCollapsed((value) => !value)}
          className="rounded p-1 hover:bg-gray-200"
          title={collapsed ? "Expand" : "Collapse"}
        >
          {collapsed ? <PanelLeftOpen size={20} /> : <PanelLeftClose size={20} />}
        </button>
      </div>

      <nav className="flex flex-col gap-1 p-2">
        <Link
          to="/hotels"
          title="Hotels"
          className={`flex items-center gap-2 rounded px-2 py-2 text-sm ${
            isHotelsActive ? "bg-blue-100 text-blue-700" : "hover:bg-gray-200"
          }`}
        >
          <Building2 size={20} />
          {!collapsed && <span>Hotels</span>}
        </Link>
      </nav>

      <div className="mt-auto flex flex-col gap-2 border-t p-2">
        <div
          className="flex items-center gap-2 px-2 py-1 text-sm"
          title={`${user.username} (${user.role})`}
        >
          <UserRound size={20} />
          {!collapsed && (
            <span className="truncate">
              {user.username} ({user.role})
            </span>
          )}
        </div>
        <button
          onClick={handleLogout}
          title="Logout"
          className="flex items-center gap-2 rounded px-2 py-2 text-sm hover:bg-gray-200"
        >
          <LogOut size={20} />
          {!collapsed && <span>Logout</span>}
        </button>
      </div>
    </aside>
  );
}
```

Save as `frontend/src/components/Sidebar.tsx`. The `if (!user) return null;` guard is defense-in-depth: `AppLayout` (Task 2) only renders `Sidebar` when a user is present, but the guard means `Sidebar` is also safe if ever rendered elsewhere, and it lets every reference below use `user.username`/`user.role` directly with no non-null assertions.

- [ ] **Step 4: Verify the build**

```bash
cd frontend && npm run build
```

Expected: 0 TypeScript errors. `Sidebar` isn't imported anywhere yet — that's fine, an unused exported component doesn't trigger `noUnusedLocals`.

- [ ] **Step 5: Commit**

```bash
git add frontend/package.json frontend/package-lock.json frontend/src/components/Sidebar.tsx
git commit -m "feat: add collapsible Sidebar component"
```

---

### Task 2: `AppLayout`, route wiring, `HomePage` cleanup, and manual verification

**Files:**
- Create: `frontend/src/components/AppLayout.tsx`
- Modify: `frontend/src/routes/AppRoutes.tsx`
- Modify: `frontend/src/pages/HomePage.tsx`

**Interfaces:**
- Consumes: `Sidebar` (Task 1), `useAuth()` → `{ user, isLoading }`.
- Produces: `AppLayout` component wrapping `AppRoutes`'s `<Routes>` — every page rendered through `AppRoutes` now goes through this auth-gated chrome.

- [ ] **Step 1: Create `AppLayout`**

```tsx
import type { ReactNode } from "react";
import { useAuth } from "../hooks/useAuth";
import { Sidebar } from "./Sidebar";

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

Save as `frontend/src/components/AppLayout.tsx`.

- [ ] **Step 2: Wire `AppLayout` into `AppRoutes`**

Replace the contents of `frontend/src/routes/AppRoutes.tsx` with:

```tsx
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
import { AppLayout } from "../components/AppLayout";

export function AppRoutes() {
  return (
    <AppLayout>
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
    </AppLayout>
  );
}
```

Only two changes from the current file: the `AppLayout` import, and `<Routes>...</Routes>` now nested inside `<AppLayout>...</AppLayout>`. No individual `<Route>` changed.

- [ ] **Step 3: Remove the redundant user/logout block from `HomePage`**

Replace the contents of `frontend/src/pages/HomePage.tsx` with:

```tsx
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { fetchHealthStatus } from "../features/health/healthService";
import { useAuth } from "../hooks/useAuth";

export function HomePage() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ["health"],
    queryFn: fetchHealthStatus,
  });
  const { user, isLoading: isAuthLoading } = useAuth();

  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-2">
      <h1 className="text-2xl font-semibold">Hotel Booking Engine</h1>

      {isLoading && <p>Checking API status...</p>}
      {isError && <p>Unable to reach the API.</p>}
      {data && <p>API status: {data.status}</p>}

      {!isAuthLoading && !user && (
        <Link to="/login" className="text-blue-600 underline">
          Login
        </Link>
      )}
    </main>
  );
}
```

The "Logged in as X (role)" text, the "Hotels" link, and the "Log out" button are gone from `HomePage` — the sidebar now covers all three (nav + user info + logout) on every authenticated page, including this one.

- [ ] **Step 4: Verify the build**

```bash
cd frontend && npm run build
```

Expected: 0 TypeScript errors.

- [ ] **Step 5: Manual end-to-end verification**

Terminal 1: `dotnet run --project backend/HotelBookingEngine.Api`
Terminal 2: `cd frontend && npm run dev`

In a browser:
1. Go to `/` while logged out → no sidebar, just the title/API status/"Login" link.
2. Go to `/login`, log in as `admin`/`Admin123!` → redirected/navigate to `/` → the sidebar now appears, expanded by default, showing "Hotels" (highlighted, since `/` does not start with `/hotels` — confirm it is *not* highlighted here) and the footer with "admin (Admin)" + "Logout".
3. Click "Hotels" in the sidebar → navigates to `/hotels`; the "Hotels" nav item is now highlighted.
4. Click into "Room Types" then "Rooms" on any row → the sidebar persists unchanged on every nested page, still showing "Hotels" highlighted.
5. Click the collapse toggle → sidebar shrinks to icon-only width; icons for "Hotels", the user avatar, and "Logout" remain, each with a hover tooltip (native `title`) showing the full label.
6. Reload the page (F5) → sidebar stays collapsed (persisted via `localStorage`).
7. Click the toggle again → expands back, still on the same page.
8. Click "Logout" from a page other than Home (e.g., `/hotels`) → logs out and navigates to `/login`; confirm no sidebar is visible on `/login`.
9. Log back in and navigate directly to `/` → confirm the sidebar is present there too (this is the behavior change from before: the sidebar is no longer conditional on which route you're viewing, only on being logged in).

Stop both processes once confirmed. This step needs a human/browser to actually execute — describe the result when reporting back rather than assuming it passed.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/components/AppLayout.tsx frontend/src/routes/AppRoutes.tsx frontend/src/pages/HomePage.tsx
git commit -m "feat: add authenticated layout with collapsible sidebar navigation"
```

---

## Self-Review Notes

- **Spec coverage:** `AppLayout` auth-gating and wrapping the whole route tree (Task 2), `Sidebar` with collapse state persisted to `localStorage` under `sidebar-collapsed` (Task 1), single "Hotels" nav item with active-route highlighting (Task 1), user/logout footer moved out of `HomePage` into the sidebar (Tasks 1-2), `lucide-react` as the new icon dependency with the specific icons named in the spec (`PanelLeftClose`, `PanelLeftOpen`, `Building2`, `UserRound`, `LogOut`) (Task 1), no backend changes, no new nav items for Room Types/Rooms (Global Constraints) are all covered.
- **Placeholder scan:** no TODO/TBD; both components are given in full, `AppRoutes.tsx` and `HomePage.tsx` are given as complete replacement contents (not diffs), consistent with how prior phase plans handled full-file replacements.
- **Type consistency:** `Sidebar` takes no props and is imported/rendered as `<Sidebar />` in `AppLayout` (Task 2) exactly as declared in Task 1. `AppLayout` takes `{ children: ReactNode }` and is used as `<AppLayout><Routes>...</Routes></AppLayout>` in `AppRoutes.tsx`, matching its declared props. `useAuth()`'s returned shape (`user`, `isLoading`, `logout`) matches `frontend/src/stores/AuthContext.tsx`'s `AuthContextValue` exactly — no new fields assumed.
