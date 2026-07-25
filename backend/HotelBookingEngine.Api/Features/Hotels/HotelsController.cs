using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelBookingEngine.Api.Features.Hotels;

[ApiController]
[Route("api/hotels")]
[Authorize]
public class HotelsController : ControllerBase
{
    private readonly IHotelService _hotelService;

    public HotelsController(IHotelService hotelService)
    {
        _hotelService = hotelService;
    }

    [HttpGet]
    public async Task<ActionResult<List<HotelResponse>>> List(CancellationToken cancellationToken)
    {
        return Ok(await _hotelService.ListAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<HotelResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var hotel = await _hotelService.GetByIdAsync(id, cancellationToken);
        return hotel is null ? NotFound() : Ok(hotel);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<HotelResponse>> Create(HotelRequest request, CancellationToken cancellationToken)
    {
        var hotel = await _hotelService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = hotel.Id }, hotel);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<HotelResponse>> Update(int id, HotelRequest request, CancellationToken cancellationToken)
    {
        var hotel = await _hotelService.UpdateAsync(id, request, cancellationToken);
        return hotel is null ? NotFound() : Ok(hotel);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _hotelService.DeleteAsync(id, cancellationToken);
        return result switch
        {
            HotelDeleteResult.Deleted => NoContent(),
            HotelDeleteResult.NotFound => NotFound(),
            HotelDeleteResult.HasRoomTypes => Conflict("Cannot delete a hotel that still has room types. Delete its room types first."),
            _ => throw new InvalidOperationException($"Unhandled {nameof(HotelDeleteResult)} value: {result}")
        };
    }
}
