using HotelBookingEngine.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HotelBookingEngine.Api.Features.Hotels;

public class HotelService : IHotelService
{
    private readonly AppDbContext _dbContext;

    public HotelService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HotelResponse> CreateAsync(HotelRequest request, CancellationToken cancellationToken)
    {
        var hotel = new Hotel
        {
            Name = request.Name,
            Address = request.Address,
            City = request.City,
            Phone = request.Phone
        };

        _dbContext.Hotels.Add(hotel);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(hotel);
    }

    public async Task<HotelResponse?> UpdateAsync(int id, HotelRequest request, CancellationToken cancellationToken)
    {
        var hotel = await _dbContext.Hotels.FindAsync([id], cancellationToken);
        if (hotel is null)
        {
            return null;
        }

        hotel.Name = request.Name;
        hotel.Address = request.Address;
        hotel.City = request.City;
        hotel.Phone = request.Phone;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(hotel);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var hotel = await _dbContext.Hotels.FindAsync([id], cancellationToken);
        if (hotel is null)
        {
            return false;
        }

        _dbContext.Hotels.Remove(hotel);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<HotelResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var hotel = await _dbContext.Hotels.FindAsync([id], cancellationToken);
        return hotel is null ? null : ToResponse(hotel);
    }

    public async Task<List<HotelResponse>> ListAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Hotels
            .OrderBy(h => h.Name)
            .Select(h => new HotelResponse(h.Id, h.Name, h.Address, h.City, h.Phone))
            .ToListAsync(cancellationToken);
    }

    private static HotelResponse ToResponse(Hotel hotel) =>
        new(hotel.Id, hotel.Name, hotel.Address, hotel.City, hotel.Phone);
}
