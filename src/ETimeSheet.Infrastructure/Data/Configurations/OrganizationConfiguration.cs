using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="Organization"/> onto the existing
/// <c>dbo.Organization</c> table, per the column list the database owner
/// supplied on 2026-10-09. No index is declared and there is no query filter -
/// the table has no soft-delete column.
/// </summary>
public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organization");

        builder.HasKey(organization => organization.OrganizationId);

        builder.Property(organization => organization.OrganizationId)
            .HasColumnName("OrganizationID")
            .ValueGeneratedOnAdd();

        // nvarchar(max).
        builder.Property(organization => organization.OrganizationName).HasColumnName("OrganizationName");

        builder.Property(organization => organization.CreateDate)
            .HasColumnName("CreateDate")
            .HasColumnType("datetimeoffset(7)");

        builder.Property(organization => organization.CountryId).HasColumnName("CountryID");
    }
}
