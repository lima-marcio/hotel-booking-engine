using Microsoft.AspNetCore.Mvc;

namespace HotelBookingEngine.Api.Features.Health;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IHealthService _healthService;

    public HealthController(IHealthService healthService)
    {
        _healthService = healthService;
    }

    [HttpGet]
    public ActionResult<HealthStatus> Get()
    {
        return Ok(_healthService.GetStatus());
    }
}
