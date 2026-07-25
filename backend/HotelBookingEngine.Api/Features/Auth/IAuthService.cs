using System.Security.Claims;

namespace HotelBookingEngine.Api.Features.Auth;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    CurrentUserResponse GetCurrentUser(ClaimsPrincipal principal);
}
