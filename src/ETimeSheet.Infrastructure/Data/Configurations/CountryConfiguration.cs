using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="Country"/> onto the existing
/// <c>dbo.Country</c> lookup table.
/// <para>
/// This is a database-first mapping: the column names and types below describe
/// a table that already exists and must match it exactly. No index is declared,
/// because indexes are owned by the database.
/// </para>
/// <para>
/// There is no query filter: the table has no soft-delete column, so every row
/// is always live.
/// </para>
/// </summary>
public class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.ToTable("Country");

        builder.HasKey(country => country.Id);

        builder.Property(country => country.Id).HasColumnName("ID");

        builder.Property(country => country.Name)
            .HasColumnName("Name")
            .HasMaxLength(80);

        builder.Property(country => country.Code)
            .HasColumnName("Code")
            .HasMaxLength(6);

        // One id, or several comma-separated - see the entity. Widened to
        // nvarchar(1000) by docs/database/data/dbo.Country_UpdateTimeZone.sql,
        // because the longest value (US, 29 zones) is 598 characters.
        builder.Property(country => country.TimeZone)
            .HasColumnName("TimeZone")
            .HasMaxLength(1000);

        // datetimeoffset, not datetime: this table differs from the rest of the
        // schema, and mapping it as datetime would throw away the offset.
        builder.Property(country => country.CreateDate)
            .HasColumnName("CreateDate")
            .HasColumnType("datetimeoffset(7)");
    }
}
