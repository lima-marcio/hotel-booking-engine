import { Routes, Route } from "react-router-dom";
import { HomePage } from "../pages/HomePage";
import { LoginPage } from "../pages/LoginPage";
import { HotelsPage } from "../pages/HotelsPage";
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
    </Routes>
  );
}
