using HotelBookingEngine.Api.Features.Hotels;
using HotelBookingEngine.Api.Features.Rooms;
using HotelBookingEngine.Api.Features.RoomTypes;
using HotelBookingEngine.Api.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelBookingEngine.Tests.Features.RoomTypes;

public class RoomTypeServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _dbContext;
    private readonly RoomTypeService _sut;

    public RoomTypeServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();

        _sut = new RoomTypeService(_dbContext);
    }

    private async Task<int> CreateHotelAsync(string name = "Grand Hotel")
    {
        var hotel = new Hotel { Name = name, Address = "123 Main St", City = "Springfield", Phone = "555-0100" };
        _dbContext.Hotels.Add(hotel);
        await _dbContext.SaveChangesAsync();
        return hotel.Id;
    }

    private static RoomTypeRequest SampleRequest(string name = "Deluxe") => new()
    {
        Name = name,
        Description = "Spacious room with a view",
        Capacity = 2,
        DailyRate = 150m
    };

    [Fact]
    public async Task CreateAsync_WithExistingHotel_PersistsAndReturnsRoomType()
    {
        var hotelId = await CreateHotelAsync();

        var result = await _sut.CreateAsync(hotelId, SampleRequest(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.Id > 0);
        Assert.Equal(hotelId, result.HotelId);
        Assert.Equal("Deluxe", result.Name);

        var stored = await _dbContext.RoomTypes.FindAsync(result.Id);
        Assert.NotNull(stored);
    }

    [Fact]
    public async Task CreateAsync_WithUnknownHotelId_ReturnsNull()
    {
        var result = await _sut.CreateAsync(999, SampleRequest(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_WithExistingId_UpdatesAndReturnsRoomType()
    {
        var hotelId = await CreateHotelAsync();
        var created = await _sut.CreateAsync(hotelId, SampleRequest(), CancellationToken.None);

        var updated = await _sut.UpdateAsync(created!.Id, SampleRequest("Suite"), CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("Suite", updated!.Name);
    }

    [Fact]
    public async Task UpdateAsync_WithUnknownId_ReturnsNull()
    {
        var result = await _sut.UpdateAsync(999, SampleRequest(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_WithExistingId_RemovesRoomTypeAndReturnsDeleted()
    {
        var hotelId = await CreateHotelAsync();
        var created = await _sut.CreateAsync(hotelId, SampleRequest(), CancellationToken.None);

        var result = await _sut.DeleteAsync(created!.Id, CancellationToken.None);

        Assert.Equal(RoomTypeDeleteResult.Deleted, result);
        Assert.Null(await _dbContext.RoomTypes.FindAsync(created.Id));
    }

    [Fact]
    public async Task DeleteAsync_WithUnknownId_ReturnsNotFound()
    {
        var result = await _sut.DeleteAsync(999, CancellationToken.None);

        Assert.Equal(RoomTypeDeleteResult.NotFound, result);
    }

    [Fact]
    public async Task DeleteAsync_WithExistingRooms_ReturnsHasRoomsAndDoesNotDelete()
    {
        var hotelId = await CreateHotelAsync();
        var created = await _sut.CreateAsync(hotelId, SampleRequest(), CancellationToken.None);
        _dbContext.Rooms.Add(new Room
        {
            RoomTypeId = created!.Id,
            HotelId = hotelId,
            RoomNumber = "101",
            Status = RoomStatus.Available
        });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.DeleteAsync(created.Id, CancellationToken.None);

        Assert.Equal(RoomTypeDeleteResult.HasRooms, result);
        Assert.NotNull(await _dbContext.RoomTypes.FindAsync(created.Id));
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsRoomType()
    {
        var hotelId = await CreateHotelAsync();
        var created = await _sut.CreateAsync(hotelId, SampleRequest(), CancellationToken.None);

        var result = await _sut.GetByIdAsync(created!.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Deluxe", result!.Name);
    }

    [Fact]
    public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
    {
        var result = await _sut.GetByIdAsync(999, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ListByHotelAsync_ReturnsOnlyThatHotelsRoomTypes()
    {
        var hotelAId = await CreateHotelAsync("Hotel A");
        var hotelBId = await CreateHotelAsync("Hotel B");

        await _sut.CreateAsync(hotelAId, SampleRequest("Standard"), CancellationToken.None);
        await _sut.CreateAsync(hotelAId, SampleRequest("Deluxe"), CancellationToken.None);
        await _sut.CreateAsync(hotelBId, SampleRequest("Suite"), CancellationToken.None);

        var result = await _sut.ListByHotelAsync(hotelAId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        Assert.All(result, rt => Assert.Equal(hotelAId, rt.HotelId));
    }

    [Fact]
    public async Task ListByHotelAsync_WithUnknownHotelId_ReturnsNull()
    {
        var result = await _sut.ListByHotelAsync(999, CancellationToken.None);

        Assert.Null(result);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }
}
