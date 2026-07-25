export interface RoomType {
  id: number;
  hotelId: number;
  name: string;
  description: string;
  capacity: number;
  dailyRate: number;
}

export interface RoomTypeRequest {
  name: string;
  description: string;
  capacity: number;
  dailyRate: number;
}
