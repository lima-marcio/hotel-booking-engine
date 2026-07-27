using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelBookingEngine.Api.Features.Rooms;

[ApiController]
[Authorize]
public class RoomsController : ControllerBase
{
    private readonly IRoomService _roomService;

    public RoomsController(IRoomService roomService)
    {
        _roomService = roomService;
    }

    [HttpGet("api/room-types/{roomTypeId:int}/rooms")]
    public async Task<ActionResult<List<RoomResponse>>> ListByRoomType(int roomTypeId, CancellationToken cancellationToken)
    {
        var rooms = await _roomService.ListByRoomTypeAsync(roomTypeId, cancellationToken);
        return rooms is null ? NotFound() : Ok(rooms);
    }

    [HttpPost("api/room-types/{roomTypeId:int}/rooms")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RoomResponse>> Create(int roomTypeId, RoomRequest request, CancellationToken cancellationToken)
    {
        var result = await _roomService.CreateAsync(roomTypeId, request, cancellationToken);
        return result.Outcome switch
        {
            RoomSaveOutcome.Success => CreatedAtAction(nameof(GetById), new { id = result.Room!.Id }, result.Room),
            RoomSaveOutcome.ParentNotFound => NotFound(),
            RoomSaveOutcome.DuplicateRoomNumber => Conflict("A room with this number already exists in this hotel."),
            _ => throw new InvalidOperationException($"Unhandled {nameof(RoomSaveOutcome)} value: {result.Outcome}")
        };
    }

    [HttpGet("api/rooms/{id:int}")]
    public async Task<ActionResult<RoomResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var room = await _roomService.GetByIdAsync(id, cancellationToken);
        return room is null ? NotFound() : Ok(room);
    }

    [HttpPut("api/rooms/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RoomResponse>> Update(int id, RoomRequest request, CancellationToken cancellationToken)
    {
        var result = await _roomService.UpdateAsync(id, request, cancellationToken);
        return result.Outcome switch
        {
            RoomSaveOutcome.Success => Ok(result.Room),
            RoomSaveOutcome.ParentNotFound => NotFound(),
            RoomSaveOutcome.DuplicateRoomNumber => Conflict("A room with this number already exists in this hotel."),
            _ => throw new InvalidOperationException($"Unhandled {nameof(RoomSaveOutcome)} value: {result.Outcome}")
        };
    }

    [HttpDelete("api/rooms/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _roomService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
