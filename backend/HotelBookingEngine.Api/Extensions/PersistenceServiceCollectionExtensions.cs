using Microsoft.EntityFrameworkCore;
using HotelBookingEngine.Api.Persistence;

namespace HotelBookingEngine.Api.Extensions;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            if (environment.IsDevelopment())
            {
                options.UseSqlite(configuration.GetConnectionString("Sqlite"));
            }
            else
            {
                options.UseSqlServer(configuration.GetConnectionString("SqlServer"));
            }
        });

        return services;
    }
}
