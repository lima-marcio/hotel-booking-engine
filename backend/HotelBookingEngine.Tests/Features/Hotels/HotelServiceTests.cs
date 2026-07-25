using HotelBookingEngine.Api.Features.Hotels;
using HotelBookingEngine.Api.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelBookingEngine.Tests.Features.Hotels;

public class HotelServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _dbContext;
    private readonly HotelService _sut;

    public HotelServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();

        _sut = new HotelService(_dbContext);
    }

    private static HotelRequest SampleRequest(string name = "Grand Hotel") => new()
    {
        Name = name,
        Address = "123 Main St",
        City = "Springfield",
        Phone = "555-0100"
    };

    [Fact]
    public async Task CreateAsync_PersistsAndReturnsHotel()
    {
        var result = await _sut.CreateAsync(SampleRequest(), CancellationToken.None);

        Assert.True(result.Id > 0);
        Assert.Equal("Grand Hotel", result.Name);

        var stored = await _dbContext.Hotels.FindAsync(result.Id);
        Assert.NotNull(stored);
        Assert.Equal("Grand Hotel", stored!.Name);
    }

    [Fact]
    public async Task UpdateAsync_WithExistingId_UpdatesAndReturnsHotel()
    {
        var created = await _sut.CreateAsync(SampleRequest(), CancellationToken.None);

        var updated = await _sut.UpdateAsync(
            created.Id, SampleRequest("Renamed Hotel"), CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("Renamed Hotel", updated!.Name);
    }

    [Fact]
    public async Task UpdateAsync_WithUnknownId_ReturnsNull()
    {
        var result = await _sut.UpdateAsync(999, SampleRequest(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_WithExistingId_RemovesHotelAndReturnsTrue()
    {
        var created = await _sut.CreateAsync(SampleRequest(), CancellationToken.None);

        var result = await _sut.DeleteAsync(created.Id, CancellationToken.None);

        Assert.True(result);
        Assert.Null(await _dbContext.Hotels.FindAsync(created.Id));
    }

    [Fact]
    public async Task DeleteAsync_WithUnknownId_ReturnsFalse()
    {
        var result = await _sut.DeleteAsync(999, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task ListAsync_ReturnsAllCreatedHotels()
    {
        await _sut.CreateAsync(SampleRequest("Hotel A"), CancellationToken.None);
        await _sut.CreateAsync(SampleRequest("Hotel B"), CancellationToken.None);

        var result = await _sut.ListAsync(CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, h => h.Name == "Hotel A");
        Assert.Contains(result, h => h.Name == "Hotel B");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }
}
