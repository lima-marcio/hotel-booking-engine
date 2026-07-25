namespace HotelBookingEngine.Api.Features.RoomTypes;

public class RoomTypeRequest
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public int Capacity { get; set; }
    public decimal DailyRate { get; set; }
}
