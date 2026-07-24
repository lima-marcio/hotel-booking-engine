using HotelBookingEngine.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.ConfigureSerilog();
builder.Services.AddPersistence(builder.Configuration, builder.Environment);

// Add services to the container.

builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseGlobalExceptionHandling();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
