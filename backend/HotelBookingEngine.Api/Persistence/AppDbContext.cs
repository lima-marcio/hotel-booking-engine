using Microsoft.EntityFrameworkCore;
using HotelBookingEngine.Api.Features.Auth;
using HotelBookingEngine.Api.Features.Hotels;
using HotelBookingEngine.Api.Features.RoomTypes;

namespace HotelBookingEngine.Api.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Hotel> Hotels => Set<Hotel>();
    public DbSet<RoomType> RoomTypes => Set<RoomType>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
