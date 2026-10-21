using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="TimesheetApproverSetup"/> onto the existing
/// <c>dbo.Timesheet_Approver_setup</c> table, per
/// <c>docs/database/schema/dbo.Timesheet_Approver_setup.sql</c>. Column names
/// are named explicitly, because <c>OrgID</c> does not match its property. No
/// index is declared, because indexes are owned by the database.
/// </summary>
public class TimesheetApproverSetupConfiguration : IEntityTypeConfiguration<TimesheetApproverSetup>
{
    public void Configure(EntityTypeBuilder<TimesheetApproverSetup> builder)
    {
        builder.ToTable("Timesheet_Approver_setup");

        builder.HasKey(setup => setup.TimesheetApproverSetupId);

        builder.Property(setup => setup.TimesheetApproverSetupId)
            .HasColumnName("TimesheetApproverSetupID")
            .ValueGeneratedOnAdd();

        builder.Property(setup => setup.ApproverId).HasColumnName("ApproverID");

        builder.Property(setup => setup.ProjectId).HasColumnName("ProjectID");

        builder.Property(setup => setup.CreateDate)
            .HasColumnName("CreateDate")
            .HasColumnType("datetime");

        builder.Property(setup => setup.CreatedBy).HasColumnName("CreatedBy");

        builder.Property(setup => setup.IsDelete).HasColumnName("IsDelete");

        builder.Property(setup => setup.DeletedDate)
            .HasColumnName("DeletedDate")
            .HasColumnType("datetime");

        builder.Property(setup => setup.DeletedBy).HasColumnName("DeletedBy");

        builder.Property(setup => setup.IsTimesheetApprover).HasColumnName("IsTimesheetApprover");

        builder.Property(setup => setup.OrganizationId).HasColumnName("OrgID");

        // Soft delete: NULL and 0 are both live, so the test is "not true"
        // rather than "is false". "!= true" would drop the NULL rows, because
        // SQL's NULL <> 1 is unknown, not true.
        builder.HasQueryFilter(setup => setup.IsDelete == null || setup.IsDelete == false);
    }
}
