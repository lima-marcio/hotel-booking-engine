import { httpClient } from "../../api/httpClient";
import type { HealthStatus } from "../../types/health";

export async function fetchHealthStatus(): Promise<HealthStatus> {
  const response = await httpClient.get<HealthStatus>("/api/health");
  return response.data;
}
