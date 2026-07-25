using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HotelBookingEngine.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HotelBookingEngine.Api.Features.Auth;

public class AuthService : IAuthService
{
    private readonly AppDbContext _dbContext;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AuthService(AppDbContext dbContext, IJwtTokenGenerator tokenGenerator, IPasswordHasher<User> passwordHasher)
    {
        _dbContext = dbContext;
        _tokenGenerator = tokenGenerator;
        _passwordHasher = passwordHasher;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var normalizedUsername = request.Username.ToLowerInvariant();
        var user = await _dbContext.Users
            .SingleOrDefaultAsync(u => u.Username.ToLower() == normalizedUsername, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return null;
        }

        var (token, expiresAtUtc) = _tokenGenerator.GenerateToken(user);
        return new LoginResponse(token, expiresAtUtc, user.Username, user.Role.ToString());
    }

    public CurrentUserResponse GetCurrentUser(ClaimsPrincipal principal)
    {
        var id = int.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var username = principal.FindFirstValue(ClaimTypes.Name)!;
        var role = principal.FindFirstValue(ClaimTypes.Role)!;

        return new CurrentUserResponse(id, username, role);
    }
}
