using HotelBookingEngine.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HotelBookingEngine.Api.Features.Rooms;

public class RoomService : IRoomService
{
    private readonly AppDbContext _dbContext;

    public RoomService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RoomSaveResult> CreateAsync(int roomTypeId, RoomRequest request, CancellationToken cancellationToken)
    {
        var roomType = await _dbContext.RoomTypes.FindAsync([roomTypeId], cancellationToken);
        if (roomType is null)
        {
            return new RoomSaveResult(RoomSaveOutcome.ParentNotFound, null);
        }

        var duplicate = await _dbContext.Rooms.AnyAsync(
            r => r.HotelId == roomType.HotelId && r.RoomNumber == request.RoomNumber, cancellationToken);
        if (duplicate)
        {
            return new RoomSaveResult(RoomSaveOutcome.DuplicateRoomNumber, null);
        }

        var room = new Room
        {
            RoomTypeId = roomTypeId,
            HotelId = roomType.HotelId,
            RoomNumber = request.RoomNumber,
            Status = request.Status
        };

        _dbContext.Rooms.Add(room);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RoomSaveResult(RoomSaveOutcome.Success, ToResponse(room));
    }

    public async Task<RoomSaveResult> UpdateAsync(int id, RoomRequest request, CancellationToken cancellationToken)
    {
        var room = await _dbContext.Rooms.FindAsync([id], cancellationToken);
        if (room is null)
        {
            return new RoomSaveResult(RoomSaveOutcome.ParentNotFound, null);
        }

        var duplicate = await _dbContext.Rooms.AnyAsync(
            r => r.Id != id && r.HotelId == room.HotelId && r.RoomNumber == request.RoomNumber, cancellationToken);
        if (duplicate)
        {
            return new RoomSaveResult(RoomSaveOutcome.DuplicateRoomNumber, null);
        }

        room.RoomNumber = request.RoomNumber;
        room.Status = request.Status;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RoomSaveResult(RoomSaveOutcome.Success, ToResponse(room));
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var room = await _dbContext.Rooms.FindAsync([id], cancellationToken);
        if (room is null)
        {
            return false;
        }

        _dbContext.Rooms.Remove(room);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<RoomResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var room = await _dbContext.Rooms.FindAsync([id], cancellationToken);
        return room is null ? null : ToResponse(room);
    }

    public async Task<List<RoomResponse>?> ListByRoomTypeAsync(int roomTypeId, CancellationToken cancellationToken)
    {
        var roomTypeExists = await _dbContext.RoomTypes.AnyAsync(rt => rt.Id == roomTypeId, cancellationToken);
        if (!roomTypeExists)
        {
            return null;
        }

        var rooms = await _dbContext.Rooms
            .Where(r => r.RoomTypeId == roomTypeId)
            .OrderBy(r => r.RoomNumber)
            .ToListAsync(cancellationToken);

        return rooms.Select(ToResponse).ToList();
    }

    private static RoomResponse ToResponse(Room room) =>
        new(room.Id, room.RoomTypeId, room.HotelId, room.RoomNumber, room.Status);
}
