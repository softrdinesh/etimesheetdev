using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="TimesheetProjectSubmissionAuditLog"/> onto
/// the <c>dbo.TimesheetProjectSubmissionAuditLog</c> table, per
/// <c>docs/database/schema/dbo.TimesheetProjectSubmissionAuditLog.sql</c>. No
/// soft delete and no query filter: the table is append-only.
/// </summary>
public class TimesheetProjectSubmissionAuditLogConfiguration : IEntityTypeConfiguration<TimesheetProjectSubmissionAuditLog>
{
    public void Configure(EntityTypeBuilder<TimesheetProjectSubmissionAuditLog> builder)
    {
        builder.ToTable("TimesheetProjectSubmissionAuditLog");

        builder.HasKey(auditLog => auditLog.TimesheetProjectSubmissionAuditLogId);

        builder.Property(auditLog => auditLog.TimesheetProjectSubmissionAuditLogId)
            .HasColumnName("TimesheetProjectSubmissionAuditLogID")
            .ValueGeneratedOnAdd();

        builder.Property(auditLog => auditLog.TimesheetProjectSubmissionId).HasColumnName("TimesheetProjectSubmissionID");

        // A relationship in the model only - the database declares no foreign
        // key. It lets a new sheet and its first audit row go in one save,
        // the sheet's identity flowing into this row's column.
        builder.HasOne(auditLog => auditLog.Submission)
            .WithMany()
            .HasForeignKey(auditLog => auditLog.TimesheetProjectSubmissionId);

        builder.Property(auditLog => auditLog.StatusId).HasColumnName("StatusID");

        builder.Property(auditLog => auditLog.CreatedBy).HasColumnName("CreatedBy");

        builder.Property(auditLog => auditLog.CreateDate)
            .HasColumnName("CreateDate")
            .HasColumnType("datetime");
    }
}
