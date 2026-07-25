using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelBookingEngine.Api.Features.Auth;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        return result is null ? Unauthorized() : Ok(result);
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<CurrentUserResponse> Me()
    {
        return Ok(_authService.GetCurrentUser(User));
    }
}
