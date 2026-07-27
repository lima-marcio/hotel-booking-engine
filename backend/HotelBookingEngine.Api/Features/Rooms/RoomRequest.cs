using System.Text.Json.Serialization;

namespace HotelBookingEngine.Api.Features.Rooms;

public class RoomRequest
{
    public required string RoomNumber { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RoomStatus Status { get; set; }
}
