namespace HotelBookingEngine.Api.Features.Auth;

public record LoginResponse(string Token, DateTime ExpiresAtUtc, string Username, string Role);
