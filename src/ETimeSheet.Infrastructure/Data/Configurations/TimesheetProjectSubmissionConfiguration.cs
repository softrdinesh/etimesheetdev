using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="TimesheetProjectSubmission"/> onto
/// <c>dbo.TimesheetProjectSubmission</c>, per
/// <c>docs/database/schema/dbo.TimesheetProjectSubmission.sql</c>. No index is
/// declared, because indexes are owned by the database.
/// </summary>
public class TimesheetProjectSubmissionConfiguration : IEntityTypeConfiguration<TimesheetProjectSubmission>
{
    public void Configure(EntityTypeBuilder<TimesheetProjectSubmission> builder)
    {
        builder.ToTable("TimesheetProjectSubmission");

        builder.HasKey(sheet => sheet.TimesheetProjectSubmissionId);

        builder.Property(sheet => sheet.TimesheetProjectSubmissionId)
            .HasColumnName("TimesheetProjectSubmissionID")
            .ValueGeneratedOnAdd();

        builder.Property(sheet => sheet.SheetSubmissionCode)
            .HasColumnName("SheetSubmissionCode")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(sheet => sheet.TotalHours)
            .HasColumnName("TotalHours")
            .HasColumnType("decimal(18,2)");

        builder.Property(sheet => sheet.TotalMinutes)
            .HasColumnName("TotalMinutes")
            .HasColumnType("decimal(18,2)");

        builder.Property(sheet => sheet.StatusId).HasColumnName("StatusID");
        builder.Property(sheet => sheet.SubmittedBy).HasColumnName("SubmittedBy");

        builder.Property(sheet => sheet.SubmittedDate)
            .HasColumnName("SubmittedDate")
            .HasColumnType("datetime");

        builder.Property(sheet => sheet.ApprovedBy).HasColumnName("ApprovedBy");

        builder.Property(sheet => sheet.ApprovedDate)
            .HasColumnName("ApprovedDate")
            .HasColumnType("datetime");

        builder.Property(sheet => sheet.RejectedBy).HasColumnName("RejectedBy");

        builder.Property(sheet => sheet.RejectedDate)
            .HasColumnName("RejectedDate")
            .HasColumnType("datetime");

        builder.Property(sheet => sheet.RejectionReason)
            .HasColumnName("RejectionReason")
            .HasMaxLength(500);

        builder.Property(sheet => sheet.IsDelete).HasColumnName("IsDelete");

        builder.HasQueryFilter(sheet => sheet.IsDelete == 0);
    }
}
