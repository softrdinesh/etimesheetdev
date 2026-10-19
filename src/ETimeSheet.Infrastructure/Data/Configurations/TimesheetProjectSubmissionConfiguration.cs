using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="TimesheetProjectSubmission"/> onto the
/// <c>dbo.TimesheetProjectSubmission</c> table, per
/// <c>docs/database/schema/dbo.TimesheetProjectSubmission.sql</c>. No index is
/// declared, because indexes are owned by the database.
/// </summary>
public class TimesheetProjectSubmissionConfiguration : IEntityTypeConfiguration<TimesheetProjectSubmission>
{
    public void Configure(EntityTypeBuilder<TimesheetProjectSubmission> builder)
    {
        builder.ToTable("TimesheetProjectSubmission");

        builder.HasKey(submission => submission.TimesheetProjectSubmissionId);

        builder.Property(submission => submission.TimesheetProjectSubmissionId)
            .HasColumnName("TimesheetProjectSubmissionID")
            .ValueGeneratedOnAdd();

        builder.Property(submission => submission.SheetSubmissionCode)
            .HasColumnName("SheetSubmissionCode")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(submission => submission.StatusId).HasColumnName("StatusID");

        builder.Property(submission => submission.UserId).HasColumnName("UserID");

        builder.Property(submission => submission.ProjectId).HasColumnName("ProjectID");

        builder.Property(submission => submission.OrganizationId).HasColumnName("OrganizationID");

        builder.Property(submission => submission.PeriodStartDate)
            .HasColumnName("PeriodStartDate")
            .HasColumnType("datetime");

        builder.Property(submission => submission.PeriodEndDate)
            .HasColumnName("PeriodEndDate")
            .HasColumnType("datetime");

        builder.Property(submission => submission.TotalHoursLogged)
            .HasColumnName("TotalHoursLogged")
            .HasColumnType("decimal(18,2)");

        builder.Property(submission => submission.TotalMinutesLogged)
            .HasColumnName("TotalMinutesLogged")
            .HasColumnType("decimal(18,2)");

        builder.Property(submission => submission.SubmittedBy).HasColumnName("SubmittedBy");

        builder.Property(submission => submission.SubmittedDate)
            .HasColumnName("SubmittedDate")
            .HasColumnType("datetime");

        builder.Property(submission => submission.ApprovedBy).HasColumnName("ApprovedBy");

        builder.Property(submission => submission.ApprovedDate)
            .HasColumnName("ApprovedDate")
            .HasColumnType("datetime");

        builder.Property(submission => submission.RejectedBy).HasColumnName("RejectedBy");

        builder.Property(submission => submission.RejectedDate)
            .HasColumnName("RejectedDate")
            .HasColumnType("datetime");

        builder.Property(submission => submission.RejectionReason)
            .HasColumnName("RejectionReason")
            .HasMaxLength(500);

        builder.Property(submission => submission.IsDelete).HasColumnName("IsDelete");

        // Lower case in the database.
        builder.Property(submission => submission.DateAdded)
            .HasColumnName("dateadded")
            .HasColumnType("datetime");

        builder.HasQueryFilter(submission => submission.IsDelete != 1);
    }
}
