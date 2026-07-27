namespace HotelBookingEngine.Api.Features.Rooms;

public enum RoomSaveOutcome
{
    Success,
    ParentNotFound,
    DuplicateRoomNumber
}

public record RoomSaveResult(RoomSaveOutcome Outcome, RoomResponse? Room);
