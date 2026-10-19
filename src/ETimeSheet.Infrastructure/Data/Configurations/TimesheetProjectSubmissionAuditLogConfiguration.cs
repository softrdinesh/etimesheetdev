using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="TimesheetProjectSubmissionAuditLog"/> onto
/// <c>dbo.TimesheetProjectSubmissionAuditLog</c>, per
/// <c>docs/database/schema/dbo.TimesheetProjectSubmissionAuditLog.sql</c>. No
/// index is declared, because indexes are owned by the database.
/// </summary>
public class TimesheetProjectSubmissionAuditLogConfiguration
    : IEntityTypeConfiguration<TimesheetProjectSubmissionAuditLog>
{
    public void Configure(EntityTypeBuilder<TimesheetProjectSubmissionAuditLog> builder)
    {
        builder.ToTable("TimesheetProjectSubmissionAuditLog");

        builder.HasKey(audit => audit.TimesheetProjectSubmissionAuditLogId);

        builder.Property(audit => audit.TimesheetProjectSubmissionAuditLogId)
            .HasColumnName("TimesheetProjectSubmissionAuditLogID")
            .ValueGeneratedOnAdd();

        builder.Property(audit => audit.TimesheetProjectSubmissionId)
            .HasColumnName("TimesheetProjectSubmissionID");

        builder.Property(audit => audit.StatusId).HasColumnName("StatusID");

        builder.Property(audit => audit.Remark)
            .HasColumnName("Remark")
            .HasMaxLength(500);

        builder.Property(audit => audit.CreatedBy).HasColumnName("CreatedBy");

        builder.Property(audit => audit.CreateDate)
            .HasColumnName("CreateDate")
            .HasColumnType("datetime");

        // No query filter: the trail has no soft delete and is never hidden.
    }
}
