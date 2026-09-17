using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="DayMaster"/> onto the existing
/// <c>dbo.DayMaster</c> lookup table.
/// <para>
/// This is a database-first mapping: the column names and types below describe
/// a table that already exists and must match it exactly. The UNIQUE
/// constraints on <c>Day</c> and <c>DayCode</c> are not declared here, because
/// indexes and constraints are owned by the database.
/// </para>
/// <para>
/// There is no query filter: the table has no soft-delete column, and every one
/// of its seven rows is always live.
/// </para>
/// </summary>
public class DayMasterConfiguration : IEntityTypeConfiguration<DayMaster>
{
    public void Configure(EntityTypeBuilder<DayMaster> builder)
    {
        builder.ToTable("DayMaster");

        builder.HasKey(day => day.DayId);

        // ValueGeneratedNever, not ValueGeneratedOnAdd: DayID is a plain INT and
        // the table is NOT an IDENTITY. Leaving EF Core to assume otherwise
        // would make it omit the column from any insert and read back a value
        // the database never produced.
        builder.Property(day => day.DayId)
            .HasColumnName("DayID")
            .ValueGeneratedNever();

        // varchar, not nvarchar: IsUnicode(false) stops EF Core sending an
        // N-prefixed parameter, which would force a conversion on the column and
        // throw away the unique index when filtering by name or code.
        builder.Property(day => day.Day)
            .HasColumnName("Day")
            .HasColumnType("varchar(20)")
            .IsUnicode(false)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(day => day.DayCode)
            .HasColumnName("DayCode")
            .HasColumnType("varchar(2)")
            .IsUnicode(false)
            .HasMaxLength(2)
            .IsRequired();
    }
}
