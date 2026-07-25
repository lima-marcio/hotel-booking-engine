import { Link } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { deleteHotel, listHotels } from "../features/hotels/hotelService";
import { useAuth } from "../hooks/useAuth";

export function HotelsPage() {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const { data, isLoading, isError } = useQuery({
    queryKey: ["hotels"],
    queryFn: listHotels,
  });

  const deleteMutation = useMutation({
    mutationFn: deleteHotel,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["hotels"] });
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
      <h1 className="text-2xl font-semibold">Hotels</h1>

      {isAdmin && (
        <Link to="/hotels/new" className="text-blue-600 underline">
          New Hotel
        </Link>
      )}

      {isLoading && <p>Loading hotels...</p>}
      {isError && <p>Unable to load hotels.</p>}

      {data && (
        <table className="w-full max-w-3xl border-collapse">
          <thead>
            <tr className="border-b text-left">
              <th className="p-2">Name</th>
              <th className="p-2">Address</th>
              <th className="p-2">City</th>
              <th className="p-2">Phone</th>
              {isAdmin && <th className="p-2">Actions</th>}
            </tr>
          </thead>
          <tbody>
            {data.map((hotel) => (
              <tr key={hotel.id} className="border-b">
                <td className="p-2">{hotel.name}</td>
                <td className="p-2">{hotel.address}</td>
                <td className="p-2">{hotel.city}</td>
                <td className="p-2">{hotel.phone}</td>
                {isAdmin && (
                  <td className="flex gap-2 p-2">
                    <Link to={`/hotels/${hotel.id}/edit`} className="text-blue-600 underline">
                      Edit
                    </Link>
                    <button
                      onClick={() => handleDelete(hotel.id, hotel.name)}
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
