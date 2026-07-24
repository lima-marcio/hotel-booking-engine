import { useQuery } from "@tanstack/react-query";
import { fetchHealthStatus } from "../features/health/healthService";

export function HomePage() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ["health"],
    queryFn: fetchHealthStatus,
  });

  if (isLoading) {
    return <p>Checking API status...</p>;
  }

  if (isError || !data) {
    return <p>Unable to reach the API.</p>;
  }

  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-2">
      <h1 className="text-2xl font-semibold">Hotel Booking Engine</h1>
      <p>API status: {data.status}</p>
    </main>
  );
}
