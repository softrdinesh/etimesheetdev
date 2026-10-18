using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="TimesheetWeekendSetup"/> onto the existing
/// <c>dbo.TimeSheet_Weekend_setup</c> table, per
/// <c>docs/database/schema/dbo.TimeSheet_Weekend_setup.sql</c>. Column names
/// are named explicitly, because <c>OrgID</c> does not match its property. No
/// index is declared, because indexes are owned by the database.
/// </summary>
public class TimesheetWeekendSetupConfiguration : IEntityTypeConfiguration<TimesheetWeekendSetup>
{
    public void Configure(EntityTypeBuilder<TimesheetWeekendSetup> builder)
    {
        builder.ToTable("TimeSheet_Weekend_setup");

        builder.HasKey(setup => setup.TimeSheetWeekendSetupId);

        builder.Property(setup => setup.TimeSheetWeekendSetupId)
            .HasColumnName("TimeSheetWeekendSetupID")
            .ValueGeneratedOnAdd();

        builder.Property(setup => setup.OrganizationId).HasColumnName("OrgID");

        builder.Property(setup => setup.ClientId).HasColumnName("ClientID");

        builder.Property(setup => setup.ProjectId).HasColumnName("ProjectID");

        builder.Property(setup => setup.IsMondayWeekend).HasColumnName("IsMondayWeekend");

        builder.Property(setup => setup.IsTuesdayWeekend).HasColumnName("IsTuesdayWeekend");

        builder.Property(setup => setup.IsWednesdayWeekend).HasColumnName("IsWednesdayWeekend");

        builder.Property(setup => setup.IsThursdayWeekend).HasColumnName("IsThursdayWeekend");

        builder.Property(setup => setup.IsFridayWeekend).HasColumnName("IsFridayWeekend");

        builder.Property(setup => setup.IsSaturdayWeekend).HasColumnName("IsSaturdayWeekend");

        builder.Property(setup => setup.IsSundayWeekend).HasColumnName("IsSundayWeekend");

        builder.Property(setup => setup.CreateDate)
            .HasColumnName("CreateDate")
            .HasColumnType("datetime");

        builder.Property(setup => setup.CreatedBy).HasColumnName("CreatedBy");

        builder.Property(setup => setup.IsDelete).HasColumnName("IsDelete");

        builder.Property(setup => setup.DeletedDate)
            .HasColumnName("DeletedDate")
            .HasColumnType("datetime");

        builder.Property(setup => setup.DeletedBy).HasColumnName("DeletedBy");

        builder.Property(setup => setup.UpdatedDate)
            .HasColumnName("UpdatedDate")
            .HasColumnType("datetime");

        builder.Property(setup => setup.UpdatedBy).HasColumnName("UpdatedBy");

        // Soft delete: NULL and 0 are both live, so the test is "not true"
        // rather than "is false". "!= true" would drop the NULL rows, because
        // SQL's NULL <> 1 is unknown, not true.
        builder.HasQueryFilter(setup => setup.IsDelete == null || setup.IsDelete == false);
    }
}
