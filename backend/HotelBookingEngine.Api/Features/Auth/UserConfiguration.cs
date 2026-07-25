using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelBookingEngine.Api.Features.Auth;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public const string SeedAdminPasswordHash =
        "AQAAAAIAAYagAAAAEFcUQUh++8uhwIPl5ZLbH9pqYcVgsjXD2wd02MD3xXjMRNh2hWOOXD3s/J8LnDygRA==";

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(u => u.Username)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .IsRequired();

        builder.Property(u => u.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasData(new User
        {
            Id = 1,
            Username = "admin",
            PasswordHash = SeedAdminPasswordHash,
            Role = Role.Admin
        });
    }
}
