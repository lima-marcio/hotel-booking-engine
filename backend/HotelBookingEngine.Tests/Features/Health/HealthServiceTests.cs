using HotelBookingEngine.Api.Features.Health;
using Xunit;

namespace HotelBookingEngine.Tests.Features.Health;

public class HealthServiceTests
{
    [Fact]
    public void GetStatus_ReturnsHealthyStatusWithCurrentUtcTimestamp()
    {
        var sut = new HealthService();
        var before = DateTime.UtcNow;

        var result = sut.GetStatus();

        var after = DateTime.UtcNow;
        Assert.Equal("Healthy", result.Status);
        Assert.InRange(result.CheckedAtUtc, before, after);
    }
}
