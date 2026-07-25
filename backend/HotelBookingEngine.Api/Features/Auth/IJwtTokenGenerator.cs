namespace HotelBookingEngine.Api.Features.Auth;

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAtUtc) GenerateToken(User user);
}
