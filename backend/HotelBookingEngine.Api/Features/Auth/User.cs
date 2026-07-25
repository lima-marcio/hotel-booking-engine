namespace HotelBookingEngine.Api.Features.Auth;

public class User
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public Role Role { get; set; }
}
