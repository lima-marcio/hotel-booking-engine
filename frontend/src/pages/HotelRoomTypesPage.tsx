import { Link, useParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { deleteRoomType, listRoomTypes } from "../features/roomTypes/roomTypeService";
import { getHotel } from "../features/hotels/hotelService";
import { useAuth } from "../hooks/useAuth";

export function HotelRoomTypesPage() {
  const { hotelId: hotelIdParam } = useParams<{ hotelId: string }>();
  const hotelId = Number(hotelIdParam);
  const { user } = useAuth();
  const queryClient = useQueryClient();

  const { data: hotel } = useQuery({
    queryKey: ["hotels", hotelId],
    queryFn: () => getHotel(hotelId),
  });

  const { data, isLoading, isError } = useQuery({
    queryKey: ["hotels", hotelId, "room-types"],
    queryFn: () => listRoomTypes(hotelId),
  });

  const deleteMutation = useMutation({
    mutationFn: deleteRoomType,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["hotels", hotelId, "room-types"] });
    },
  });

  const isAdmin = user?.role === "Admin";

  function handleDelete(id: number, name: string) {
    if (window.confirm(`Delete "${name}"?`)) {
      deleteMutation.mutate(id);
    }
  }

  return (
    <main className="flex min-h-screen flex-col items-center gap-4 p-8">
      <Link to="/hotels" className="text-blue-600 underline">
        Back to Hotels
      </Link>
      <h1 className="text-2xl font-semibold">Room Types{hotel ? ` — ${hotel.name}` : ""}</h1>

      {isAdmin && (
        <Link to={`/hotels/${hotelId}/room-types/new`} className="text-blue-600 underline">
          New Room Type
        </Link>
      )}

      {isLoading && <p>Loading room types...</p>}
      {isError && <p>Unable to load room types.</p>}

      {data && (
        <table className="w-full max-w-3xl border-collapse">
          <thead>
            <tr className="border-b text-left">
              <th className="p-2">Name</th>
              <th className="p-2">Description</th>
              <th className="p-2">Capacity</th>
              <th className="p-2">Daily Rate</th>
              {isAdmin && <th className="p-2">Actions</th>}
            </tr>
          </thead>
          <tbody>
            {data.map((roomType) => (
              <tr key={roomType.id} className="border-b">
                <td className="p-2">{roomType.name}</td>
                <td className="p-2">{roomType.description}</td>
                <td className="p-2">{roomType.capacity}</td>
                <td className="p-2">{roomType.dailyRate}</td>
                {isAdmin && (
                  <td className="flex gap-2 p-2">
                    <Link to={`/hotels/${hotelId}/room-types/${roomType.id}/edit`} className="text-blue-600 underline">
                      Edit
                    </Link>
                    <button
                      onClick={() => handleDelete(roomType.id, roomType.name)}
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
