using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="TimesheetUserSelfieAttendance"/> onto the
/// <c>dbo.TimesheetUserSelfieAttendance</c> table, per
/// <c>docs/database/schema/dbo.TimesheetUserSelfieAttendance.sql</c>. No index
/// is declared, because indexes are owned by the database. The table has no
/// soft-delete column, so there is no query filter.
/// </summary>
public class TimesheetUserSelfieAttendanceConfiguration : IEntityTypeConfiguration<TimesheetUserSelfieAttendance>
{
    private const int TextMaximumLength = 500;

    public void Configure(EntityTypeBuilder<TimesheetUserSelfieAttendance> builder)
    {
        builder.ToTable("TimesheetUserSelfieAttendance");

        builder.HasKey(attendance => attendance.AttendanceId);

        builder.Property(attendance => attendance.AttendanceId)
            .HasColumnName("AttendanceID")
            .ValueGeneratedOnAdd();

        builder.Property(attendance => attendance.UserId).HasColumnName("UserID");

        builder.Property(attendance => attendance.ProjectId).HasColumnName("ProjectID");

        builder.Property(attendance => attendance.OrganizationId).HasColumnName("OrganizationID");

        builder.Property(attendance => attendance.CheckInDateTimeUtc)
            .HasColumnName("CheckInDateTimeUTC")
            .HasColumnType("datetime2(0)");

        builder.Property(attendance => attendance.CheckInSelfiePath)
            .HasColumnName("CheckInSelfiePath")
            .HasMaxLength(TextMaximumLength)
            .IsRequired();

        builder.Property(attendance => attendance.CheckInLatitude)
            .HasColumnName("CheckInLatitude")
            .HasMaxLength(TextMaximumLength);

        builder.Property(attendance => attendance.CheckInLongitude)
            .HasColumnName("CheckInLongitude")
            .HasMaxLength(TextMaximumLength);

        builder.Property(attendance => attendance.CheckOutDateTimeUtc)
            .HasColumnName("CheckOutDateTimeUTC")
            .HasColumnType("datetime2(0)");

        builder.Property(attendance => attendance.CheckOutSelfiePath)
            .HasColumnName("CheckOutSelfiePath")
            .HasMaxLength(TextMaximumLength);

        builder.Property(attendance => attendance.CheckOutLatitude)
            .HasColumnName("CheckOutLatitude")
            .HasMaxLength(TextMaximumLength);

        builder.Property(attendance => attendance.CheckOutLongitude)
            .HasColumnName("CheckOutLongitude")
            .HasMaxLength(TextMaximumLength);

        builder.Property(attendance => attendance.LoggedMinutes).HasColumnName("LoggedMinutes");

        builder.Property(attendance => attendance.CreateDate)
            .HasColumnName("CreateDate")
            .HasColumnType("datetime2(0)");

        builder.Property(attendance => attendance.CreatedBy).HasColumnName("CreatedBy");

        builder.Property(attendance => attendance.UpdateDate)
            .HasColumnName("UpdateDate")
            .HasColumnType("datetime2(0)");

        builder.Property(attendance => attendance.UpdatedBy).HasColumnName("UpdatedBy");
    }
}
