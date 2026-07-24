using HotelBookingEngine.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerWithJwt();
builder.Services.AddPersistence(builder.Configuration, builder.Environment);
builder.Services.AddApplicationServices();
builder.Services.AddFrontendCorsPolicy(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseGlobalExceptionHandling();
app.UseHttpsRedirection();
app.UseCors(HotelBookingEngine.Api.Extensions.CorsServiceCollectionExtensions.FrontendPolicyName);
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program
{
}
