using HotelBookingEngine.Api.Features.Hotels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelBookingEngine.Api.Features.RoomTypes;

public class RoomTypeConfiguration : IEntityTypeConfiguration<RoomType>
{
    public void Configure(EntityTypeBuilder<RoomType> builder)
    {
        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(rt => rt.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(rt => rt.Capacity)
            .IsRequired();

        builder.Property(rt => rt.DailyRate)
            .IsRequired();

        builder.HasOne<Hotel>()
            .WithMany()
            .HasForeignKey(rt => rt.HotelId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
