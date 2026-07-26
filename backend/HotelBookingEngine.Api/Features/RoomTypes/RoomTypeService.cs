using HotelBookingEngine.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HotelBookingEngine.Api.Features.RoomTypes;

public class RoomTypeService : IRoomTypeService
{
    private readonly AppDbContext _dbContext;

    public RoomTypeService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RoomTypeResponse?> CreateAsync(int hotelId, RoomTypeRequest request, CancellationToken cancellationToken)
    {
        var hotelExists = await _dbContext.Hotels.AnyAsync(h => h.Id == hotelId, cancellationToken);
        if (!hotelExists)
        {
            return null;
        }

        var roomType = new RoomType
        {
            HotelId = hotelId,
            Name = request.Name,
            Description = request.Description,
            Capacity = request.Capacity,
            DailyRate = request.DailyRate
        };

        _dbContext.RoomTypes.Add(roomType);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(roomType);
    }

    public async Task<RoomTypeResponse?> UpdateAsync(int id, RoomTypeRequest request, CancellationToken cancellationToken)
    {
        var roomType = await _dbContext.RoomTypes.FindAsync([id], cancellationToken);
        if (roomType is null)
        {
            return null;
        }

        roomType.Name = request.Name;
        roomType.Description = request.Description;
        roomType.Capacity = request.Capacity;
        roomType.DailyRate = request.DailyRate;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(roomType);
    }

    public async Task<RoomTypeDeleteResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var roomType = await _dbContext.RoomTypes.FindAsync([id], cancellationToken);
        if (roomType is null)
        {
            return RoomTypeDeleteResult.NotFound;
        }

        var hasRooms = await _dbContext.Rooms.AnyAsync(r => r.RoomTypeId == id, cancellationToken);
        if (hasRooms)
        {
            return RoomTypeDeleteResult.HasRooms;
        }

        _dbContext.RoomTypes.Remove(roomType);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return RoomTypeDeleteResult.Deleted;
    }

    public async Task<RoomTypeResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var roomType = await _dbContext.RoomTypes.FindAsync([id], cancellationToken);
        return roomType is null ? null : ToResponse(roomType);
    }

    public async Task<List<RoomTypeResponse>?> ListByHotelAsync(int hotelId, CancellationToken cancellationToken)
    {
        var hotelExists = await _dbContext.Hotels.AnyAsync(h => h.Id == hotelId, cancellationToken);
        if (!hotelExists)
        {
            return null;
        }

        return await _dbContext.RoomTypes
            .Where(rt => rt.HotelId == hotelId)
            .OrderBy(rt => rt.Name)
            .Select(rt => new RoomTypeResponse(rt.Id, rt.HotelId, rt.Name, rt.Description, rt.Capacity, rt.DailyRate))
            .ToListAsync(cancellationToken);
    }

    private static RoomTypeResponse ToResponse(RoomType roomType) =>
        new(roomType.Id, roomType.HotelId, roomType.Name, roomType.Description, roomType.Capacity, roomType.DailyRate);
}
