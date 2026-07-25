using HotelBookingEngine.Api.Features.Auth;
using HotelBookingEngine.Api.Features.Health;
using HotelBookingEngine.Api.Features.Hotels;
using HotelBookingEngine.Api.Features.RoomTypes;
using Microsoft.AspNetCore.Identity;

namespace HotelBookingEngine.Api.Extensions;

public static class ApplicationServicesCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IHealthService, HealthService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IHotelService, HotelService>();
        services.AddScoped<IRoomTypeService, RoomTypeService>();

        return services;
    }
}
