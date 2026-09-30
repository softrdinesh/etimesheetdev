using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="TimeSheetSubmissionAuditLog"/> onto the
/// existing <c>dbo.TimeSheetSubmissionAuditLog</c> table.
/// <para>
/// This is a database-first mapping: the column names and types below must
/// match <c>docs/database/schema/dbo.TimeSheetSubmissionAuditLog.sql</c>
/// exactly. No index is declared, because indexes are owned by the database.
/// </para>
/// </summary>
public class TimeSheetSubmissionAuditLogConfiguration : IEntityTypeConfiguration<TimeSheetSubmissionAuditLog>
{
    public void Configure(EntityTypeBuilder<TimeSheetSubmissionAuditLog> builder)
    {
        builder.ToTable("TimeSheetSubmissionAuditLog");

        builder.HasKey(auditLog => auditLog.TimeSheetSubmissionAuditLogId);

        // IDENTITY: a sequence number the database generates, so it is left out
        // of every insert and read back after the save.
        builder.Property(auditLog => auditLog.TimeSheetSubmissionAuditLogId)
            .HasColumnName("TimeSheetSubmissionAuditLogID")
            .ValueGeneratedOnAdd();

        // A sheet code, varchar - not an int, whatever the name says.
        builder.Property(auditLog => auditLog.TimeSheetSubmissionId)
            .HasColumnName("TimeSheetSubmissionID")
            .HasMaxLength(20)
            .IsUnicode(false);

        builder.Property(auditLog => auditLog.StatusId).HasColumnName("StatusID");

        builder.Property(auditLog => auditLog.CreatedBy).HasColumnName("CreatedBy");

        builder.Property(auditLog => auditLog.CreateDate)
            .HasColumnName("CreateDate")
            .HasColumnType("datetime");
    }
}
