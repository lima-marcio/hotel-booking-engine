using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HotelBookingEngine.Api.Features.Auth;
using HotelBookingEngine.Api.Features.Hotels;
using HotelBookingEngine.Api.Features.RoomTypes;
using HotelBookingEngine.Api.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace HotelBookingEngine.Tests.Features.RoomTypes;

public class RoomTypesEndpointsTests : IDisposable
{
    private const string ReceptionistUsername = "receptionist-test";
    private const string ReceptionistPassword = "Reception123!";

    private readonly string _dbPath;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public RoomTypesEndpointsTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"hotelbookingengine-roomtypes-tests-{Guid.NewGuid():N}.db");

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={_dbPath}"));
            });
        });

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Database.Migrate();

            var passwordHasher = new PasswordHasher<User>();
            dbContext.Users.Add(new User
            {
                Username = ReceptionistUsername,
                PasswordHash = passwordHasher.HashPassword(null!, ReceptionistPassword),
                Role = Role.Receptionist
            });
            dbContext.SaveChanges();
        }

        _client = _factory.CreateClient();
    }

    private async Task<string> LoginAsync(string username, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { Username = username, Password = password });
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.Token;
    }

    private void AuthorizeAs(string token)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<int> CreateHotelAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/hotels", new
        {
            Name = "Grand Hotel",
            Address = "123 Main St",
            City = "Springfield",
            Phone = "555-0100"
        });
        var hotel = await response.Content.ReadFromJsonAsync<HotelResponse>();
        return hotel!.Id;
    }

    private static object SampleRequestBody(string name = "Deluxe") => new
    {
        Name = name,
        Description = "Spacious room with a view",
        Capacity = 2,
        DailyRate = 150m
    };

    [Fact]
    public async Task ListByHotel_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/hotels/1/room-types");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_AsAdmin_ReturnsOkWithRoomType()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();
        var created = await (await _client.PostAsJsonAsync($"/api/hotels/{hotelId}/room-types", SampleRequestBody()))
            .Content.ReadFromJsonAsync<RoomTypeResponse>();

        var response = await _client.GetAsync($"/api/room-types/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var roomType = await response.Content.ReadFromJsonAsync<RoomTypeResponse>();
        Assert.Equal(created.Id, roomType!.Id);
        Assert.Equal(created.Name, roomType.Name);
    }

    [Fact]
    public async Task ListByHotel_WithUnknownHotelId_ReturnsNotFound()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));

        var response = await _client.GetAsync("/api/hotels/999/room-types");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithUnknownHotelId_ReturnsNotFound()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));

        var response = await _client.PostAsJsonAsync("/api/hotels/999/room-types", SampleRequestBody());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListByHotel_AsReceptionist_ReturnsOk()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();

        AuthorizeAs(await LoginAsync(ReceptionistUsername, ReceptionistPassword));
        var response = await _client.GetAsync($"/api/hotels/{hotelId}/room-types");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsReceptionist_ReturnsForbidden()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();

        AuthorizeAs(await LoginAsync(ReceptionistUsername, ReceptionistPassword));
        var response = await _client.PostAsJsonAsync($"/api/hotels/{hotelId}/room-types", SampleRequestBody());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsAdmin_ReturnsCreatedAndThenListIncludesIt()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();

        var createResponse = await _client.PostAsJsonAsync($"/api/hotels/{hotelId}/room-types", SampleRequestBody());
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<RoomTypeResponse>();
        Assert.NotNull(created);
        Assert.Equal("Deluxe", created!.Name);
        Assert.Equal(hotelId, created.HotelId);

        var listResponse = await _client.GetAsync($"/api/hotels/{hotelId}/room-types");
        var roomTypes = await listResponse.Content.ReadFromJsonAsync<List<RoomTypeResponse>>();
        Assert.Contains(roomTypes!, rt => rt.Id == created.Id);
    }

    [Fact]
    public async Task Update_AsReceptionist_ReturnsForbidden()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();
        var created = await (await _client.PostAsJsonAsync($"/api/hotels/{hotelId}/room-types", SampleRequestBody()))
            .Content.ReadFromJsonAsync<RoomTypeResponse>();

        AuthorizeAs(await LoginAsync(ReceptionistUsername, ReceptionistPassword));
        var response = await _client.PutAsJsonAsync($"/api/room-types/{created!.Id}", SampleRequestBody("Updated Name"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_AsAdmin_ReturnsOkWithUpdatedData()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();
        var created = await (await _client.PostAsJsonAsync($"/api/hotels/{hotelId}/room-types", SampleRequestBody()))
            .Content.ReadFromJsonAsync<RoomTypeResponse>();

        var response = await _client.PutAsJsonAsync($"/api/room-types/{created!.Id}", SampleRequestBody("Updated Deluxe"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<RoomTypeResponse>();
        Assert.Equal("Updated Deluxe", updated!.Name);
        Assert.Equal(created.Id, updated.Id);
    }

    [Fact]
    public async Task Delete_AsReceptionist_ReturnsForbidden()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();
        var created = await (await _client.PostAsJsonAsync($"/api/hotels/{hotelId}/room-types", SampleRequestBody()))
            .Content.ReadFromJsonAsync<RoomTypeResponse>();

        AuthorizeAs(await LoginAsync(ReceptionistUsername, ReceptionistPassword));
        var response = await _client.DeleteAsync($"/api/room-types/{created!.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsAdmin_RemovesRoomType()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();
        var created = await (await _client.PostAsJsonAsync($"/api/hotels/{hotelId}/room-types", SampleRequestBody()))
            .Content.ReadFromJsonAsync<RoomTypeResponse>();

        var deleteResponse = await _client.DeleteAsync($"/api/room-types/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/api/room-types/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        SqliteConnection.ClearAllPools();

        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
