import { Link, useParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { deleteRoom, listRooms } from "../features/rooms/roomService";
import { getRoomType } from "../features/roomTypes/roomTypeService";
import { getHotel } from "../features/hotels/hotelService";
import { useAuth } from "../hooks/useAuth";

export function RoomTypeRoomsPage() {
  const { hotelId: hotelIdParam, roomTypeId: roomTypeIdParam } = useParams<{ hotelId: string; roomTypeId: string }>();
  const hotelId = Number(hotelIdParam);
  const roomTypeId = Number(roomTypeIdParam);
  const { user } = useAuth();
  const queryClient = useQueryClient();

  const { data: hotel } = useQuery({
    queryKey: ["hotels", hotelId],
    queryFn: () => getHotel(hotelId),
  });

  const { data: roomType } = useQuery({
    queryKey: ["room-types", roomTypeId],
    queryFn: () => getRoomType(roomTypeId),
  });

  const { data, isLoading, isError } = useQuery({
    queryKey: ["room-types", roomTypeId, "rooms"],
    queryFn: () => listRooms(roomTypeId),
  });

  const deleteMutation = useMutation({
    mutationFn: deleteRoom,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["room-types", roomTypeId, "rooms"] });
    },
  });

  const isAdmin = user?.role === "Admin";

  function handleDelete(id: number, roomNumber: string) {
    if (window.confirm(`Delete room "${roomNumber}"?`)) {
      deleteMutation.mutate(id);
    }
  }

  return (
    <main className="flex min-h-screen flex-col items-center gap-4 p-8">
      <Link to={`/hotels/${hotelId}/room-types`} className="text-blue-600 underline">
        Back to Room Types
      </Link>
      <h1 className="text-2xl font-semibold">
        Rooms{roomType ? ` — ${roomType.name}` : ""}{hotel ? ` (${hotel.name})` : ""}
      </h1>

      {isAdmin && (
        <Link to={`/hotels/${hotelId}/room-types/${roomTypeId}/rooms/new`} className="text-blue-600 underline">
          New Room
        </Link>
      )}

      {isLoading && <p>Loading rooms...</p>}
      {isError && <p>Unable to load rooms.</p>}

      {data && (
        <table className="w-full max-w-3xl border-collapse">
          <thead>
            <tr className="border-b text-left">
              <th className="p-2">Room Number</th>
              <th className="p-2">Status</th>
              {isAdmin && <th className="p-2">Actions</th>}
            </tr>
          </thead>
          <tbody>
            {data.map((room) => (
              <tr key={room.id} className="border-b">
                <td className="p-2">{room.roomNumber}</td>
                <td className="p-2">{room.status}</td>
                {isAdmin && (
                  <td className="flex gap-2 p-2">
                    <Link
                      to={`/hotels/${hotelId}/room-types/${roomTypeId}/rooms/${room.id}/edit`}
                      className="text-blue-600 underline"
                    >
                      Edit
                    </Link>
                    <button
                      onClick={() => handleDelete(room.id, room.roomNumber)}
                      className="text-red-600 underline"
                    >
                      Delete
                    </button>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </main>
  );
}
