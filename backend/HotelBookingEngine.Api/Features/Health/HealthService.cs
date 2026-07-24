namespace HotelBookingEngine.Api.Features.Health;

public class HealthService : IHealthService
{
    public HealthStatus GetStatus()
    {
        return new HealthStatus("Healthy", DateTime.UtcNow);
    }
}
