using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelBookingEngine.Api.Features.RoomTypes;

[ApiController]
[Authorize]
public class RoomTypesController : ControllerBase
{
    private readonly IRoomTypeService _roomTypeService;

    public RoomTypesController(IRoomTypeService roomTypeService)
    {
        _roomTypeService = roomTypeService;
    }

    [HttpGet("api/hotels/{hotelId:int}/room-types")]
    public async Task<ActionResult<List<RoomTypeResponse>>> ListByHotel(int hotelId, CancellationToken cancellationToken)
    {
        var roomTypes = await _roomTypeService.ListByHotelAsync(hotelId, cancellationToken);
        return roomTypes is null ? NotFound() : Ok(roomTypes);
    }

    [HttpPost("api/hotels/{hotelId:int}/room-types")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RoomTypeResponse>> Create(int hotelId, RoomTypeRequest request, CancellationToken cancellationToken)
    {
        var roomType = await _roomTypeService.CreateAsync(hotelId, request, cancellationToken);
        return roomType is null ? NotFound() : CreatedAtAction(nameof(GetById), new { id = roomType.Id }, roomType);
    }

    [HttpGet("api/room-types/{id:int}")]
    public async Task<ActionResult<RoomTypeResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var roomType = await _roomTypeService.GetByIdAsync(id, cancellationToken);
        return roomType is null ? NotFound() : Ok(roomType);
    }

    [HttpPut("api/room-types/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RoomTypeResponse>> Update(int id, RoomTypeRequest request, CancellationToken cancellationToken)
    {
        var roomType = await _roomTypeService.UpdateAsync(id, request, cancellationToken);
        return roomType is null ? NotFound() : Ok(roomType);
    }

    [HttpDelete("api/room-types/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _roomTypeService.DeleteAsync(id, cancellationToken);
        return result == RoomTypeDeleteResult.Deleted ? NoContent() : NotFound();
    }
}
