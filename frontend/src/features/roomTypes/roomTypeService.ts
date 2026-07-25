import { httpClient } from "../../api/httpClient";
import type { RoomType, RoomTypeRequest } from "../../types/roomType";

export async function listRoomTypes(hotelId: number): Promise<RoomType[]> {
  const response = await httpClient.get<RoomType[]>(`/api/hotels/${hotelId}/room-types`);
  return response.data;
}

export async function getRoomType(id: number): Promise<RoomType> {
  const response = await httpClient.get<RoomType>(`/api/room-types/${id}`);
  return response.data;
}

export async function createRoomType(hotelId: number, request: RoomTypeRequest): Promise<RoomType> {
  const response = await httpClient.post<RoomType>(`/api/hotels/${hotelId}/room-types`, request);
  return response.data;
}

export async function updateRoomType(id: number, request: RoomTypeRequest): Promise<RoomType> {
  const response = await httpClient.put<RoomType>(`/api/room-types/${id}`, request);
  return response.data;
}

export async function deleteRoomType(id: number): Promise<void> {
  await httpClient.delete(`/api/room-types/${id}`);
}
