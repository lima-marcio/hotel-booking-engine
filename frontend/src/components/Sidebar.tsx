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
      className={`flex h-screen flex-col border-r bg-gray-50 transition-all sticky top-0 ${collapsed ? "w-14" : "w-56"}`}
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
