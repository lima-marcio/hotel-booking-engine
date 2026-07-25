using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HotelBookingEngine.Api.Features.Auth;
using HotelBookingEngine.Api.Features.Hotels;
using HotelBookingEngine.Api.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace HotelBookingEngine.Tests.Features.Hotels;

public class HotelsEndpointsTests : IDisposable
{
    private const string ReceptionistUsername = "receptionist-test";
    private const string ReceptionistPassword = "Reception123!";

    private readonly string _dbPath;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public HotelsEndpointsTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"hotelbookingengine-hotels-tests-{Guid.NewGuid():N}.db");

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

    private static object SampleRequestBody(string name = "Grand Hotel") => new
    {
        Name = name,
        Address = "123 Main St",
        City = "Springfield",
        Phone = "555-0100"
    };

    [Fact]
    public async Task List_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/hotels");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_AsReceptionist_ReturnsOk()
    {
        AuthorizeAs(await LoginAsync(ReceptionistUsername, ReceptionistPassword));

        var response = await _client.GetAsync("/api/hotels");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsReceptionist_ReturnsForbidden()
    {
        AuthorizeAs(await LoginAsync(ReceptionistUsername, ReceptionistPassword));

        var response = await _client.PostAsJsonAsync("/api/hotels", SampleRequestBody());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsAdmin_ReturnsCreatedAndThenListIncludesIt()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));

        var createResponse = await _client.PostAsJsonAsync("/api/hotels", SampleRequestBody());
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<HotelResponse>();
        Assert.NotNull(created);
        Assert.Equal("Grand Hotel", created!.Name);

        var listResponse = await _client.GetAsync("/api/hotels");
        var hotels = await listResponse.Content.ReadFromJsonAsync<List<HotelResponse>>();
        Assert.Contains(hotels!, h => h.Id == created.Id);
    }

    [Fact]
    public async Task Update_AsReceptionist_ReturnsForbidden()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var created = await (await _client.PostAsJsonAsync("/api/hotels", SampleRequestBody()))
            .Content.ReadFromJsonAsync<HotelResponse>();

        AuthorizeAs(await LoginAsync(ReceptionistUsername, ReceptionistPassword));
        var response = await _client.PutAsJsonAsync($"/api/hotels/{created!.Id}", SampleRequestBody("Updated Name"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsReceptionist_ReturnsForbidden()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var created = await (await _client.PostAsJsonAsync("/api/hotels", SampleRequestBody()))
            .Content.ReadFromJsonAsync<HotelResponse>();

        AuthorizeAs(await LoginAsync(ReceptionistUsername, ReceptionistPassword));
        var response = await _client.DeleteAsync($"/api/hotels/{created!.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsAdmin_RemovesHotel()
    {
        AuthorizeAs(await LoginAsync("admin", "Admin123!"));
        var created = await (await _client.PostAsJsonAsync("/api/hotels", SampleRequestBody()))
            .Content.ReadFromJsonAsync<HotelResponse>();

        var deleteResponse = await _client.DeleteAsync($"/api/hotels/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/api/hotels/{created.Id}");
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
