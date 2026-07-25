namespace HotelBookingEngine.Api.Features.RoomTypes;

public record RoomTypeResponse(int Id, int HotelId, string Name, string Description, int Capacity, decimal DailyRate);
