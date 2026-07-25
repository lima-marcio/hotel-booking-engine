using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelBookingEngine.Api.Features.Hotels;

public class HotelConfiguration : IEntityTypeConfiguration<Hotel>
{
    public void Configure(EntityTypeBuilder<Hotel> builder)
    {
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(h => h.Address)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(h => h.City)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(h => h.Phone)
            .IsRequired()
            .HasMaxLength(20);
    }
}
