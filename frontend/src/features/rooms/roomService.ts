import { httpClient } from "../../api/httpClient";
import type { Room, RoomRequest } from "../../types/room";

export async function listRooms(roomTypeId: number): Promise<Room[]> {
  const response = await httpClient.get<Room[]>(`/api/room-types/${roomTypeId}/rooms`);
  return response.data;
}

export async function getRoom(id: number): Promise<Room> {
  const response = await httpClient.get<Room>(`/api/rooms/${id}`);
  return response.data;
}

export async function createRoom(roomTypeId: number, request: RoomRequest): Promise<Room> {
  const response = await httpClient.post<Room>(`/api/room-types/${roomTypeId}/rooms`, request);
  return response.data;
}

export async function updateRoom(id: number, request: RoomRequest): Promise<Room> {
  const response = await httpClient.put<Room>(`/api/rooms/${id}`, request);
  return response.data;
}

export async function deleteRoom(id: number): Promise<void> {
  await httpClient.delete(`/api/rooms/${id}`);
}
