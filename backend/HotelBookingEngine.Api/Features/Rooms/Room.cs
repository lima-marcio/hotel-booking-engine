namespace HotelBookingEngine.Api.Features.Rooms;

public class Room
{
    public int Id { get; set; }
    public int RoomTypeId { get; set; }
    public int HotelId { get; set; }
    public required string RoomNumber { get; set; }
    public RoomStatus Status { get; set; }
}
