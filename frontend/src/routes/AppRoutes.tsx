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
  );
}
