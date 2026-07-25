import { httpClient } from "../../api/httpClient";
import type { Hotel, HotelRequest } from "../../types/hotel";

export async function listHotels(): Promise<Hotel[]> {
  const response = await httpClient.get<Hotel[]>("/api/hotels");
  return response.data;
}

export async function getHotel(id: number): Promise<Hotel> {
  const response = await httpClient.get<Hotel>(`/api/hotels/${id}`);
  return response.data;
}

export async function createHotel(request: HotelRequest): Promise<Hotel> {
  const response = await httpClient.post<Hotel>("/api/hotels", request);
  return response.data;
}

export async function updateHotel(id: number, request: HotelRequest): Promise<Hotel> {
  const response = await httpClient.put<Hotel>(`/api/hotels/${id}`, request);
  return response.data;
}

export async function deleteHotel(id: number): Promise<void> {
  await httpClient.delete(`/api/hotels/${id}`);
}
