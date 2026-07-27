using HotelBookingEngine.Api.Features.Hotels;
using HotelBookingEngine.Api.Features.RoomTypes;
using HotelBookingEngine.Api.Features.Rooms;
using HotelBookingEngine.Api.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelBookingEngine.Tests.Features.Rooms;

public class RoomServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _dbContext;
    private readonly RoomService _sut;

    public RoomServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();

        _sut = new RoomService(_dbContext);
    }

    private async Task<int> CreateHotelAsync(string name = "Grand Hotel")
    {
        var hotel = new Hotel { Name = name, Address = "123 Main St", City = "Springfield", Phone = "555-0100" };
        _dbContext.Hotels.Add(hotel);
        await _dbContext.SaveChangesAsync();
        return hotel.Id;
    }

    private async Task<int> CreateRoomTypeAsync(int hotelId, string name = "Deluxe")
    {
        var roomType = new RoomType
        {
            HotelId = hotelId,
            Name = name,
            Description = "Spacious room with a view",
            Capacity = 2,
            DailyRate = 150m
        };
        _dbContext.RoomTypes.Add(roomType);
        await _dbContext.SaveChangesAsync();
        return roomType.Id;
    }

    private static RoomRequest SampleRequest(string roomNumber = "101") => new()
    {
        RoomNumber = roomNumber,
        Status = RoomStatus.Available
    };

    [Fact]
    public async Task CreateAsync_WithExistingRoomType_PersistsAndReturnsRoomWithHotelIdFromRoomType()
    {
        var hotelId = await CreateHotelAsync();
        var roomTypeId = await CreateRoomTypeAsync(hotelId);

        var result = await _sut.CreateAsync(roomTypeId, SampleRequest(), CancellationToken.None);

        Assert.Equal(RoomSaveOutcome.Success, result.Outcome);
        Assert.NotNull(result.Room);
        Assert.True(result.Room!.Id > 0);
        Assert.Equal(roomTypeId, result.Room.RoomTypeId);
        Assert.Equal(hotelId, result.Room.HotelId);
        Assert.Equal("101", result.Room.RoomNumber);

        var stored = await _dbContext.Rooms.FindAsync(result.Room.Id);
        Assert.NotNull(stored);
    }

    [Fact]
    public async Task CreateAsync_WithUnknownRoomTypeId_ReturnsParentNotFound()
    {
        var result = await _sut.CreateAsync(999, SampleRequest(), CancellationToken.None);

        Assert.Equal(RoomSaveOutcome.ParentNotFound, result.Outcome);
        Assert.Null(result.Room);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateRoomNumberInSameHotel_ReturnsDuplicateRoomNumber()
    {
        var hotelId = await CreateHotelAsync();
        var roomTypeAId = await CreateRoomTypeAsync(hotelId, "Standard");
        var roomTypeBId = await CreateRoomTypeAsync(hotelId, "Deluxe");
        await _sut.CreateAsync(roomTypeAId, SampleRequest("101"), CancellationToken.None);

        var result = await _sut.CreateAsync(roomTypeBId, SampleRequest("101"), CancellationToken.None);

        Assert.Equal(RoomSaveOutcome.DuplicateRoomNumber, result.Outcome);
        Assert.Null(result.Room);
    }

    [Fact]
    public async Task CreateAsync_WithSameRoomNumberInDifferentHotel_Succeeds()
    {
        var hotelAId = await CreateHotelAsync("Hotel A");
        var hotelBId = await CreateHotelAsync("Hotel B");
        var roomTypeAId = await CreateRoomTypeAsync(hotelAId);
        var roomTypeBId = await CreateRoomTypeAsync(hotelBId);
        await _sut.CreateAsync(roomTypeAId, SampleRequest("101"), CancellationToken.None);

        var result = await _sut.CreateAsync(roomTypeBId, SampleRequest("101"), CancellationToken.None);

        Assert.Equal(RoomSaveOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task UpdateAsync_WithExistingId_UpdatesAndReturnsRoom()
    {
        var hotelId = await CreateHotelAsync();
        var roomTypeId = await CreateRoomTypeAsync(hotelId);
        var created = await _sut.CreateAsync(roomTypeId, SampleRequest(), CancellationToken.None);

        var updated = await _sut.UpdateAsync(
            created.Room!.Id,
            new RoomRequest { RoomNumber = "102", Status = RoomStatus.Maintenance },
            CancellationToken.None);

        Assert.Equal(RoomSaveOutcome.Success, updated.Outcome);
        Assert.Equal("102", updated.Room!.RoomNumber);
        Assert.Equal(RoomStatus.Maintenance, updated.Room.Status);
    }

    [Fact]
    public async Task UpdateAsync_WithUnknownId_ReturnsParentNotFound()
    {
        var result = await _sut.UpdateAsync(999, SampleRequest(), CancellationToken.None);

        Assert.Equal(RoomSaveOutcome.ParentNotFound, result.Outcome);
    }

    [Fact]
    public async Task UpdateAsync_WithRoomNumberCollidingInSameHotel_ReturnsDuplicateRoomNumber()
    {
        var hotelId = await CreateHotelAsync();
        var roomTypeId = await CreateRoomTypeAsync(hotelId);
        await _sut.CreateAsync(roomTypeId, SampleRequest("101"), CancellationToken.None);
        var second = await _sut.CreateAsync(roomTypeId, SampleRequest("102"), CancellationToken.None);

        var result = await _sut.UpdateAsync(second.Room!.Id, SampleRequest("101"), CancellationToken.None);

        Assert.Equal(RoomSaveOutcome.DuplicateRoomNumber, result.Outcome);
    }

    [Fact]
    public async Task UpdateAsync_KeepingItsOwnRoomNumber_Succeeds()
    {
        var hotelId = await CreateHotelAsync();
        var roomTypeId = await CreateRoomTypeAsync(hotelId);
        var created = await _sut.CreateAsync(roomTypeId, SampleRequest("101"), CancellationToken.None);

        var result = await _sut.UpdateAsync(
            created.Room!.Id,
            new RoomRequest { RoomNumber = "101", Status = RoomStatus.Maintenance },
            CancellationToken.None);

        Assert.Equal(RoomSaveOutcome.Success, result.Outcome);
        Assert.Equal("101", result.Room!.RoomNumber);
        Assert.Equal(RoomStatus.Maintenance, result.Room.Status);
    }

    [Fact]
    public async Task UpdateAsync_WithRoomNumberUsedInDifferentHotel_Succeeds()
    {
        var hotelAId = await CreateHotelAsync("Hotel A");
        var hotelBId = await CreateHotelAsync("Hotel B");
        var roomTypeAId = await CreateRoomTypeAsync(hotelAId);
        var roomTypeBId = await CreateRoomTypeAsync(hotelBId);
        await _sut.CreateAsync(roomTypeAId, SampleRequest("101"), CancellationToken.None);
        var second = await _sut.CreateAsync(roomTypeBId, SampleRequest("102"), CancellationToken.None);

        var result = await _sut.UpdateAsync(second.Room!.Id, SampleRequest("101"), CancellationToken.None);

        Assert.Equal(RoomSaveOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task DeleteAsync_WithExistingId_RemovesRoomAndReturnsTrue()
    {
        var hotelId = await CreateHotelAsync();
        var roomTypeId = await CreateRoomTypeAsync(hotelId);
        var created = await _sut.CreateAsync(roomTypeId, SampleRequest(), CancellationToken.None);

        var result = await _sut.DeleteAsync(created.Room!.Id, CancellationToken.None);

        Assert.True(result);
        Assert.Null(await _dbContext.Rooms.FindAsync(created.Room.Id));
    }

    [Fact]
    public async Task DeleteAsync_WithUnknownId_ReturnsFalse()
    {
        var result = await _sut.DeleteAsync(999, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsRoom()
    {
        var hotelId = await CreateHotelAsync();
        var roomTypeId = await CreateRoomTypeAsync(hotelId);
        var created = await _sut.CreateAsync(roomTypeId, SampleRequest(), CancellationToken.None);

        var result = await _sut.GetByIdAsync(created.Room!.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("101", result!.RoomNumber);
    }

    [Fact]
    public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
    {
        var result = await _sut.GetByIdAsync(999, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ListByRoomTypeAsync_ReturnsOnlyThatRoomTypesRooms()
    {
        var hotelId = await CreateHotelAsync();
        var roomTypeAId = await CreateRoomTypeAsync(hotelId, "Standard");
        var roomTypeBId = await CreateRoomTypeAsync(hotelId, "Deluxe");
        await _sut.CreateAsync(roomTypeAId, SampleRequest("101"), CancellationToken.None);
        await _sut.CreateAsync(roomTypeAId, SampleRequest("102"), CancellationToken.None);
        await _sut.CreateAsync(roomTypeBId, SampleRequest("201"), CancellationToken.None);

        var result = await _sut.ListByRoomTypeAsync(roomTypeAId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        Assert.All(result, r => Assert.Equal(roomTypeAId, r.RoomTypeId));
    }

    [Fact]
    public async Task ListByRoomTypeAsync_WithUnknownRoomTypeId_ReturnsNull()
    {
        var result = await _sut.ListByRoomTypeAsync(999, CancellationToken.None);

        Assert.Null(result);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }
}
