using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HotelBookingEngine.Api.Features.Auth;
using HotelBookingEngine.Api.Features.Hotels;
using HotelBookingEngine.Api.Features.RoomTypes;
using HotelBookingEngine.Api.Features.Rooms;
using HotelBookingEngine.Api.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace HotelBookingEngine.Tests.Features.Rooms;

public class RoomsEndpointsTests : IDisposable
{
    private const string ReceptionistUsername = "receptionist-test";
    private const string ReceptionistPassword = "Reception123!";

    private readonly string _dbPath;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public RoomsEndpointsTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"hotelbookingengine-rooms-tests-{Guid.NewGuid():N}.db");

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

    private async Task<int> CreateHotelAsync(string name = "Grand Hotel")
    {
        var response = await _client.PostAsJsonAsync("/api/hotels", new
        {
            Name = name,
            Address = "123 Main St",
            City = "Springfield",
            Phone = "555-0100"
        });
        var hotel = await response.Content.ReadFromJsonAsync<HotelResponse>();
        return hotel!.Id;
    }

    private async Task<int> CreateRoomTypeAsync(int hotelId, string name = "Deluxe")
    {
        var response = await _client.PostAsJsonAsync($"/api/hotels/{hotelId}/room-types", new
        {
            Name = name,
            Description = "Spacious room with a view",
            Capacity = 2,
            DailyRate = 150m
        });
        var roomType = await response.Content.ReadFromJsonAsync<RoomTypeResponse>();
        return roomType!.Id;
    }

    private static object SampleRequestBody(string roomNumber = "101") => new
    {
        RoomNumber = roomNumber,
        Status = "Available"
    };

    [Fact]
    public async Task ListByRoomType_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/room-types/1/rooms");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_AsAdmin_ReturnsOkWithRoom()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();
        var roomTypeId = await CreateRoomTypeAsync(hotelId);
        var created = await (await _client.PostAsJsonAsync($"/api/room-types/{roomTypeId}/rooms", SampleRequestBody()))
            .Content.ReadFromJsonAsync<RoomResponse>();

        var response = await _client.GetAsync($"/api/rooms/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var room = await response.Content.ReadFromJsonAsync<RoomResponse>();
        Assert.Equal(created.Id, room!.Id);
        Assert.Equal(created.RoomNumber, room.RoomNumber);
    }

    [Fact]
    public async Task ListByRoomType_WithUnknownRoomTypeId_ReturnsNotFound()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));

        var response = await _client.GetAsync("/api/room-types/999/rooms");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithUnknownRoomTypeId_ReturnsNotFound()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));

        var response = await _client.PostAsJsonAsync("/api/room-types/999/rooms", SampleRequestBody());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListByRoomType_AsReceptionist_ReturnsOk()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();
        var roomTypeId = await CreateRoomTypeAsync(hotelId);

        AuthorizeAs(await LoginAsync(ReceptionistUsername, ReceptionistPassword));
        var response = await _client.GetAsync($"/api/room-types/{roomTypeId}/rooms");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsReceptionist_ReturnsForbidden()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();
        var roomTypeId = await CreateRoomTypeAsync(hotelId);

        AuthorizeAs(await LoginAsync(ReceptionistUsername, ReceptionistPassword));
        var response = await _client.PostAsJsonAsync($"/api/room-types/{roomTypeId}/rooms", SampleRequestBody());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsAdmin_ReturnsCreatedWithHotelIdAndThenListIncludesIt()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();
        var roomTypeId = await CreateRoomTypeAsync(hotelId);

        var createResponse = await _client.PostAsJsonAsync($"/api/room-types/{roomTypeId}/rooms", SampleRequestBody());
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<RoomResponse>();
        Assert.NotNull(created);
        Assert.Equal("101", created!.RoomNumber);
        Assert.Equal(roomTypeId, created.RoomTypeId);
        Assert.Equal(hotelId, created.HotelId);

        var listResponse = await _client.GetAsync($"/api/room-types/{roomTypeId}/rooms");
        var rooms = await listResponse.Content.ReadFromJsonAsync<List<RoomResponse>>();
        Assert.Contains(rooms!, r => r.Id == created.Id);
    }

    [Fact]
    public async Task Create_WithDuplicateRoomNumberInSameHotel_ReturnsConflict()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();
        var roomTypeAId = await CreateRoomTypeAsync(hotelId, "Standard");
        var roomTypeBId = await CreateRoomTypeAsync(hotelId, "Deluxe");
        await _client.PostAsJsonAsync($"/api/room-types/{roomTypeAId}/rooms", SampleRequestBody("101"));

        var response = await _client.PostAsJsonAsync($"/api/room-types/{roomTypeBId}/rooms", SampleRequestBody("101"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_AsReceptionist_ReturnsForbidden()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();
        var roomTypeId = await CreateRoomTypeAsync(hotelId);
        var created = await (await _client.PostAsJsonAsync($"/api/room-types/{roomTypeId}/rooms", SampleRequestBody()))
            .Content.ReadFromJsonAsync<RoomResponse>();

        AuthorizeAs(await LoginAsync(ReceptionistUsername, ReceptionistPassword));
        var response = await _client.PutAsJsonAsync($"/api/rooms/{created!.Id}", SampleRequestBody("102"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_AsAdmin_ReturnsOkWithUpdatedData()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();
        var roomTypeId = await CreateRoomTypeAsync(hotelId);
        var created = await (await _client.PostAsJsonAsync($"/api/room-types/{roomTypeId}/rooms", SampleRequestBody()))
            .Content.ReadFromJsonAsync<RoomResponse>();

        var response = await _client.PutAsJsonAsync($"/api/rooms/{created!.Id}", SampleRequestBody("102"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<RoomResponse>();
        Assert.Equal("102", updated!.RoomNumber);
        Assert.Equal(created.Id, updated.Id);
    }

    [Fact]
    public async Task Delete_AsReceptionist_ReturnsForbidden()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();
        var roomTypeId = await CreateRoomTypeAsync(hotelId);
        var created = await (await _client.PostAsJsonAsync($"/api/room-types/{roomTypeId}/rooms", SampleRequestBody()))
            .Content.ReadFromJsonAsync<RoomResponse>();

        AuthorizeAs(await LoginAsync(ReceptionistUsername, ReceptionistPassword));
        var response = await _client.DeleteAsync($"/api/rooms/{created!.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsAdmin_RemovesRoom()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var hotelId = await CreateHotelAsync();
        var roomTypeId = await CreateRoomTypeAsync(hotelId);
        var created = await (await _client.PostAsJsonAsync($"/api/room-types/{roomTypeId}/rooms", SampleRequestBody()))
            .Content.ReadFromJsonAsync<RoomResponse>();

        var deleteResponse = await _client.DeleteAsync($"/api/rooms/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/api/rooms/{created.Id}");
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
