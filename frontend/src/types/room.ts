export type RoomStatus = "Available" | "Maintenance";

export interface Room {
  id: number;
  roomTypeId: number;
  hotelId: number;
  roomNumber: string;
  status: RoomStatus;
}

export interface RoomRequest {
  roomNumber: string;
  status: RoomStatus;
}
