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
