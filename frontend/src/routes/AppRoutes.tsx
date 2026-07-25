import { Routes, Route } from "react-router-dom";
import { HomePage } from "../pages/HomePage";
import { LoginPage } from "../pages/LoginPage";
import { HotelsPage } from "../pages/HotelsPage";
import { HotelFormPage } from "../pages/HotelFormPage";
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
    </Routes>
  );
}
