namespace HotelBookingEngine.Api.Features.RoomTypes;

public interface IRoomTypeService
{
    Task<RoomTypeResponse?> CreateAsync(int hotelId, RoomTypeRequest request, CancellationToken cancellationToken);
    Task<RoomTypeResponse?> UpdateAsync(int id, RoomTypeRequest request, CancellationToken cancellationToken);
    Task<RoomTypeDeleteResult> DeleteAsync(int id, CancellationToken cancellationToken);
    Task<RoomTypeResponse?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<List<RoomTypeResponse>?> ListByHotelAsync(int hotelId, CancellationToken cancellationToken);
}
