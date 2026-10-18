using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="TimesheetHolidaySetup"/> onto the existing
/// <c>dbo.TimesheetHolidaysetup</c> table, per
/// <c>docs/database/schema/dbo.TimesheetHolidaysetup.sql</c>. Column names
/// are irregularly cased on the table, so each is named explicitly. No index is
/// declared, because indexes are owned by the database.
/// </summary>
public class TimesheetHolidaySetupConfiguration : IEntityTypeConfiguration<TimesheetHolidaySetup>
{
    public void Configure(EntityTypeBuilder<TimesheetHolidaySetup> builder)
    {
        builder.ToTable("TimesheetHolidaysetup");

        builder.HasKey(holiday => holiday.TimesheetHolidaySetupId);

        builder.Property(holiday => holiday.TimesheetHolidaySetupId)
            .HasColumnName("TimesheetHolidaysetupID")
            .ValueGeneratedOnAdd();

        builder.Property(holiday => holiday.HolidayDate)
            .HasColumnName("HolidayDate")
            .HasColumnType("date");

        builder.Property(holiday => holiday.Description)
            .HasColumnName("Description")
            .HasMaxLength(100);

        builder.Property(holiday => holiday.OrganizationId).HasColumnName("OrganizationID");

        builder.Property(holiday => holiday.CountryId).HasColumnName("CountryID");

        builder.Property(holiday => holiday.ClientId).HasColumnName("ClientID");

        builder.Property(holiday => holiday.ProjectId).HasColumnName("ProjectID");

        builder.Property(holiday => holiday.CreateDate)
            .HasColumnName("CreateDate")
            .HasColumnType("datetime");

        builder.Property(holiday => holiday.CreatedBy).HasColumnName("CreatedBy");

        builder.Property(holiday => holiday.IsDelete).HasColumnName("IsDelete");

        // Soft delete: NULL and 0 are both live, so the test is "not true"
        // rather than "is false". "!= true" would drop the NULL rows, because
        // SQL's NULL <> 1 is unknown, not true.
        builder.HasQueryFilter(holiday => holiday.IsDelete == null || holiday.IsDelete == false);
    }
}
