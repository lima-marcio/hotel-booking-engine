namespace HotelBookingEngine.Api.Features.Hotels;

public interface IHotelService
{
    Task<HotelResponse> CreateAsync(HotelRequest request, CancellationToken cancellationToken);
    Task<HotelResponse?> UpdateAsync(int id, HotelRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
    Task<HotelResponse?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<List<HotelResponse>> ListAsync(CancellationToken cancellationToken);
}
