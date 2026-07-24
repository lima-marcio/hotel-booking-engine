namespace HotelBookingEngine.Api.Extensions;

public static class CorsServiceCollectionExtensions
{
    public const string FrontendPolicyName = "FrontendPolicy";

    public static IServiceCollection AddFrontendCorsPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options =>
        {
            options.AddPolicy(FrontendPolicyName, policy =>
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        return services;
    }
}
