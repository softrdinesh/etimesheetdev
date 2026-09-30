using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="TimesheetSubmission"/> onto the existing
/// <c>dbo.TimesheetSubmission</c> table.
/// <para>
/// This is a database-first mapping: the column names and types below describe
/// a table that already exists and must match
/// <c>docs/database/schema/dbo.TimesheetSubmission.sql</c> exactly. No index is
/// declared, because indexes are owned by the database.
/// </para>
/// <para>
/// There is no query filter: the table has no soft-delete column.
/// </para>
/// </summary>
public class TimesheetSubmissionConfiguration : IEntityTypeConfiguration<TimesheetSubmission>
{
    public void Configure(EntityTypeBuilder<TimesheetSubmission> builder)
    {
        builder.ToTable("TimesheetSubmission");

        // The table's primary key: one submission per sheet code.
        builder.HasKey(submission => submission.Timesheetcode);

        // varchar, not nvarchar.
        builder.Property(submission => submission.Timesheetcode)
            .HasColumnName("Timesheetcode")
            .HasMaxLength(15)
            .IsUnicode(false);

        builder.Property(submission => submission.TotalHours)
            .HasColumnName("TotalHours")
            .HasPrecision(18, 2);

        builder.Property(submission => submission.TotalMins)
            .HasColumnName("TotalMins")
            .HasPrecision(18, 2);

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

        builder.Property(submission => submission.StatusId).HasColumnName("StatusID");
    }
}
