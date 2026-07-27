using System.Text.Json.Serialization;

namespace HotelBookingEngine.Api.Features.Rooms;

public record RoomResponse(
    int Id,
    int RoomTypeId,
    int HotelId,
    string RoomNumber,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] RoomStatus Status);
