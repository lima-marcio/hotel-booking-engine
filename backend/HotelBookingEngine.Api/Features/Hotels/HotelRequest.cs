namespace HotelBookingEngine.Api.Features.Hotels;

public class HotelRequest
{
    public required string Name { get; set; }
    public required string Address { get; set; }
    public required string City { get; set; }
    public required string Phone { get; set; }
}
