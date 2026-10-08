using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="Signup"/> onto the existing <c>dbo.Signup</c>
/// table, per the column list the database owner supplied on 2026-10-08.
/// <c>sp_help</c> reports nvarchar lengths in bytes, so every length below is
/// half the figure it printed.
/// <para>
/// <c>Pwd</c>, <c>ResetOTP</c> and <c>OtpExpireAt</c> are not mapped - see the
/// entity. No index is declared, and there is no query filter: callers say
/// <c>IsDelete == 0</c> where they mean live users.
/// </para>
/// </summary>
public class SignupConfiguration : IEntityTypeConfiguration<Signup>
{
    public void Configure(EntityTypeBuilder<Signup> builder)
    {
        builder.ToTable("Signup");

        builder.HasKey(user => user.UserId);

        builder.Property(user => user.UserId)
            .HasColumnName("UserID")
            .ValueGeneratedOnAdd();

        builder.Property(user => user.Name).HasColumnName("Name").HasMaxLength(100);
        builder.Property(user => user.Email).HasColumnName("Email").HasMaxLength(50);
        builder.Property(user => user.AccountSource).HasColumnName("Accountsource").HasMaxLength(10);
        builder.Property(user => user.CreateDate).HasColumnName("CreateDate").HasColumnType("datetimeoffset(7)");
        builder.Property(user => user.CountryId).HasColumnName("CountryID");
        builder.Property(user => user.Latitude).HasColumnName("Latitude").HasMaxLength(20);

        // The database's spelling.
        builder.Property(user => user.Longitude).HasColumnName("Longtitude").HasMaxLength(20);

        builder.Property(user => user.UpdatedDate).HasColumnName("UpdatedDate").HasColumnType("datetimeoffset(7)");
        builder.Property(user => user.UpdateBy).HasColumnName("UpdateBy");
        builder.Property(user => user.OrganizationName).HasColumnName("OrganizationName").HasMaxLength(50);
        builder.Property(user => user.OrganizationSize).HasColumnName("OrganizationSize").HasMaxLength(15);
        builder.Property(user => user.Address).HasColumnName("Address");
        builder.Property(user => user.OrganizationId).HasColumnName("OrganizationID");
        builder.Property(user => user.IsDelete).HasColumnName("IsDelete");
        builder.Property(user => user.ProfilePicture).HasColumnName("ProfilePicture").HasMaxLength(200);
        builder.Property(user => user.IsProductOwner).HasColumnName("IsProductOwner");
        builder.Property(user => user.RoleId).HasColumnName("RoleID");
    }
}
