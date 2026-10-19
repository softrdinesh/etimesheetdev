using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="TimesheetProjectTimeLog"/> onto
/// <c>dbo.TimesheetProjectTimeLog</c>, per
/// <c>docs/database/schema/dbo.TimesheetProjectTimeLog.sql</c>. No index is
/// declared, because indexes are owned by the database.
/// </summary>
public class TimesheetProjectTimeLogConfiguration : IEntityTypeConfiguration<TimesheetProjectTimeLog>
{
    public void Configure(EntityTypeBuilder<TimesheetProjectTimeLog> builder)
    {
        builder.ToTable("TimesheetProjectTimeLog");

        builder.HasKey(entry => entry.TimesheetProjectTimeLogId);

        builder.Property(entry => entry.TimesheetProjectTimeLogId)
            .HasColumnName("TimesheetProjectTimeLogID")
            .ValueGeneratedOnAdd();

        builder.Property(entry => entry.UserId).HasColumnName("UserID");
        builder.Property(entry => entry.ProjectId).HasColumnName("ProjectID");

        builder.Property(entry => entry.SheetSubmissionCode)
            .HasColumnName("SheetSubmissionCode")
            .HasMaxLength(20)
            .IsRequired();

        // A date column: the time part is always midnight.
        builder.Property(entry => entry.LogDate)
            .HasColumnName("LogDate")
            .HasColumnType("date");

        builder.Property(entry => entry.StartTime)
            .HasColumnName("StartTime")
            .HasColumnType("time(7)");

        builder.Property(entry => entry.EndTime)
            .HasColumnName("EndTime")
            .HasColumnType("time(7)");

        builder.Property(entry => entry.DurationMinutes).HasColumnName("DurationMinutes");

        builder.Property(entry => entry.Description).HasColumnName("Description");

        builder.Property(entry => entry.Status).HasColumnName("Status");
        builder.Property(entry => entry.IsDelete).HasColumnName("IsDelete");

        builder.Property(entry => entry.CreateDate)
            .HasColumnName("CreateDate")
            .HasColumnType("datetime");

        builder.Property(entry => entry.CreatedBy).HasColumnName("CreatedBy");

        builder.Property(entry => entry.UpdateDate)
            .HasColumnName("UpdateDate")
            .HasColumnType("datetime");

        builder.Property(entry => entry.UpdatedBy).HasColumnName("UpdatedBy");

        builder.Property(entry => entry.DeletedDate)
            .HasColumnName("DeletedDate")
            .HasColumnType("datetime");

        builder.Property(entry => entry.DeletedBy).HasColumnName("DeletedBy");

        // Soft delete. The column is NOT NULL, so unlike the other tables in
        // this module there is no NULL to treat as live.
        builder.HasQueryFilter(entry => entry.IsDelete == 0);
    }
}
