using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="TimesheetProjectTimeLog"/> onto the
/// <c>dbo.TimesheetProjectTimeLog</c> table, per
/// <c>docs/database/schema/dbo.TimesheetProjectTimeLog.sql</c>. No index is
/// declared, because indexes are owned by the database.
/// </summary>
public class TimesheetProjectTimeLogConfiguration : IEntityTypeConfiguration<TimesheetProjectTimeLog>
{
    public void Configure(EntityTypeBuilder<TimesheetProjectTimeLog> builder)
    {
        builder.ToTable("TimesheetProjectTimeLog");

        builder.HasKey(timeLog => timeLog.TimesheetProjectTimeLogId);

        builder.Property(timeLog => timeLog.TimesheetProjectTimeLogId)
            .HasColumnName("TimesheetProjectTimeLogID")
            .ValueGeneratedOnAdd();

        builder.Property(timeLog => timeLog.TimesheetProjectSubmissionId).HasColumnName("TimesheetProjectSubmissionID");

        builder.Property(timeLog => timeLog.SheetSubmissionCode)
            .HasColumnName("SheetSubmissionCode")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(timeLog => timeLog.LogDate)
            .HasColumnName("LogDate")
            .HasColumnType("date");

        builder.Property(timeLog => timeLog.StartTime)
            .HasColumnName("StartTime")
            .HasColumnType("time(7)");

        builder.Property(timeLog => timeLog.EndTime)
            .HasColumnName("EndTime")
            .HasColumnType("time(7)");

        builder.Property(timeLog => timeLog.DurationMinutes).HasColumnName("DurationMinutes");

        builder.Property(timeLog => timeLog.Description).HasColumnName("Description");

        builder.Property(timeLog => timeLog.Status).HasColumnName("Status");

        builder.Property(timeLog => timeLog.IsDelete).HasColumnName("IsDelete");

        builder.Property(timeLog => timeLog.CreateDate)
            .HasColumnName("CreateDate")
            .HasColumnType("datetime");

        builder.Property(timeLog => timeLog.CreatedBy).HasColumnName("CreatedBy");

        builder.Property(timeLog => timeLog.UpdateDate)
            .HasColumnName("UpdateDate")
            .HasColumnType("datetime");

        builder.Property(timeLog => timeLog.UpdatedBy).HasColumnName("UpdatedBy");

        builder.Property(timeLog => timeLog.DeletedDate)
            .HasColumnName("DeletedDate")
            .HasColumnType("datetime");

        builder.Property(timeLog => timeLog.DeletedBy).HasColumnName("DeletedBy");

        builder.HasQueryFilter(timeLog => timeLog.IsDelete != 1);
    }
}
