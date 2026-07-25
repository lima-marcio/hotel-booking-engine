namespace HotelBookingEngine.Api.Features.RoomTypes;

public class RoomType
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public int Capacity { get; set; }
    public decimal DailyRate { get; set; }
}
