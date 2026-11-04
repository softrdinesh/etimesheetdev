using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="TimesheetProjectTimeLogStatusLookup"/> onto
/// the <c>dbo.TimesheetProjectTimeLogStatus_Lookup</c> table, per
/// <c>docs/database/schema/dbo.TimesheetProjectTimeLogStatus_Lookup.sql</c>.
/// <para>
/// <c>StatusID</c> is the key and is never generated: the ids are written by
/// hand, so they can match what <c>dbo.TimesheetProjectTimeLog.StatusID</c>
/// already holds. No index is declared, because indexes are owned by the
/// database.
/// </para>
/// </summary>
public class TimesheetProjectTimeLogStatusLookupConfiguration
    : IEntityTypeConfiguration<TimesheetProjectTimeLogStatusLookup>
{
    public void Configure(EntityTypeBuilder<TimesheetProjectTimeLogStatusLookup> builder)
    {
        builder.ToTable("TimesheetProjectTimeLogStatus_Lookup");

        builder.HasKey(status => status.StatusId);

        builder.Property(status => status.StatusId)
            .HasColumnName("StatusID")
            .ValueGeneratedNever();

        builder.Property(status => status.StatusName)
            .HasColumnName("StatusName")
            .HasMaxLength(50)
            .IsRequired();
    }
}
