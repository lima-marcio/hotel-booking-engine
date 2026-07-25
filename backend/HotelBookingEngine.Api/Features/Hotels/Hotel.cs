namespace HotelBookingEngine.Api.Features.Hotels;

public class Hotel
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Address { get; set; }
    public required string City { get; set; }
    public required string Phone { get; set; }
}
