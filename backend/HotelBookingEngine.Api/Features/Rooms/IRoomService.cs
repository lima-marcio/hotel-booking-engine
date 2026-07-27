namespace HotelBookingEngine.Api.Features.Rooms;

public interface IRoomService
{
    Task<RoomSaveResult> CreateAsync(int roomTypeId, RoomRequest request, CancellationToken cancellationToken);
    Task<RoomSaveResult> UpdateAsync(int id, RoomRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
    Task<RoomResponse?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<List<RoomResponse>?> ListByRoomTypeAsync(int roomTypeId, CancellationToken cancellationToken);
}
