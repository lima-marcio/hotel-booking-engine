using HotelBookingEngine.Api.Extensions;
using HotelBookingEngine.Api.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerWithJwt();
builder.Services.AddPersistence(builder.Configuration, builder.Environment);
builder.Services.AddApplicationServices();
builder.Services.AddFrontendCorsPolicy(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.UseGlobalExceptionHandling();
app.UseHttpsRedirection();
app.UseCors(FrontendCorsServiceCollectionExtensions.FrontendPolicyName);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program
{
}
