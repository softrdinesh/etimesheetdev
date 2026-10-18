using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="TimesheetUserProjectSetup"/> onto the
/// existing <c>dbo.Timesheet_Users_Project_setup</c> table, per
/// <c>docs/database/schema/dbo.Timesheet_Users_Project_setup.sql</c>. Column
/// names mix conventions on the table, so each is named explicitly. No index
/// is declared, because indexes are owned by the database.
/// </summary>
public class TimesheetUserProjectSetupConfiguration : IEntityTypeConfiguration<TimesheetUserProjectSetup>
{
    public void Configure(EntityTypeBuilder<TimesheetUserProjectSetup> builder)
    {
        builder.ToTable("Timesheet_Users_Project_setup");

        builder.HasKey(setup => setup.TimesheetUserProjectSetupId);

        builder.Property(setup => setup.TimesheetUserProjectSetupId)
            .HasColumnName("TimesheetUserProjectSetupID")
            .ValueGeneratedOnAdd();

        builder.Property(setup => setup.UserId).HasColumnName("UserID");
        builder.Property(setup => setup.ProjectRoleId).HasColumnName("ProjectRoleID");

        builder.Property(setup => setup.CostRate)
            .HasColumnName("Cost_rate_per_hour")
            .HasColumnType("decimal(18,2)");

        builder.Property(setup => setup.BillRate)
            .HasColumnName("Billable_rate_per_hour")
            .HasColumnType("decimal(18,2)");

        builder.Property(setup => setup.CreateDate)
            .HasColumnName("CreateDate")
            .HasColumnType("datetime");

        builder.Property(setup => setup.CreatedBy).HasColumnName("Createdby");
        builder.Property(setup => setup.IsDelete).HasColumnName("IsDelete");

        builder.Property(setup => setup.DeletedDate)
            .HasColumnName("DeletedDate")
            .HasColumnType("datetime");

        builder.Property(setup => setup.DeletedBy).HasColumnName("DeletedBy");
        builder.Property(setup => setup.JobType).HasColumnName("Jobtype");
        builder.Property(setup => setup.WorkLocationRange).HasColumnName("WorklocationRange");
        builder.Property(setup => setup.ProjectId).HasColumnName("ProjectID");

        builder.Property(setup => setup.ProjectLocationLatitude)
            .HasColumnName("ProjectlocLatitude")
            .HasMaxLength(25);

        // The database's spelling.
        builder.Property(setup => setup.ProjectLocationLongitude)
            .HasColumnName("ProjectlocLongtitude")
            .HasMaxLength(25);

        builder.Property(setup => setup.MaxLogHoursPerDay).HasColumnName("Max_log_hour_day");

        // Soft delete: the procedure writes 0, the API's remove writes 1.
        builder.HasQueryFilter(setup => setup.IsDelete == null || setup.IsDelete == 0);
    }
}
