using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="TimesheetLeaveStatusLookup"/> onto the
/// existing <c>dbo.TimesheetLeaveStatus_Lookup</c> table, per
/// <c>docs/database/schema/dbo.TimesheetLeaveStatus_Lookup.sql</c>.
/// <para>
/// <c>ID</c> is the EF key. No primary key was reported on the table, and the
/// ids are supplied by the seed rows, not generated.
/// </para>
/// </summary>
public class TimesheetLeaveStatusLookupConfiguration : IEntityTypeConfiguration<TimesheetLeaveStatusLookup>
{
    public void Configure(EntityTypeBuilder<TimesheetLeaveStatusLookup> builder)
    {
        builder.ToTable("TimesheetLeaveStatus_Lookup");

        builder.HasKey(status => status.Id);

        builder.Property(status => status.Id)
            .HasColumnName("ID")
            .ValueGeneratedNever();

        builder.Property(status => status.Value).HasColumnName("Value");

        builder.Property(status => status.Text)
            .HasColumnName("Text")
            .HasMaxLength(50);
    }
}
