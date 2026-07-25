import { httpClient } from "../../api/httpClient";
import type { CurrentUser, LoginRequest, LoginResponse } from "../../types/auth";

export async function login(credentials: LoginRequest): Promise<LoginResponse> {
  const response = await httpClient.post<LoginResponse>("/api/auth/login", credentials);
  return response.data;
}

export async function fetchCurrentUser(): Promise<CurrentUser> {
  const response = await httpClient.get<CurrentUser>("/api/auth/me");
  return response.data;
}
