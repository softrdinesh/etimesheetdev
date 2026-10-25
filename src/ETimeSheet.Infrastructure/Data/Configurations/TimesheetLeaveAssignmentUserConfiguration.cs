using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="TimesheetLeaveAssignmentUser"/> onto the
/// existing <c>dbo.Timesheet_Leave_assignment_user</c> table, per
/// <c>docs/database/schema/dbo.Timesheet_Leave_assignment_user.sql</c>. Column
/// names are named explicitly, because <c>LeavetypeID</c> is cased differently
/// from its property. No index is declared, because indexes are owned by the
/// database.
/// </summary>
public class TimesheetLeaveAssignmentUserConfiguration : IEntityTypeConfiguration<TimesheetLeaveAssignmentUser>
{
    public void Configure(EntityTypeBuilder<TimesheetLeaveAssignmentUser> builder)
    {
        builder.ToTable("Timesheet_Leave_assignment_user");

        builder.HasKey(assignment => assignment.TimesheetLeaveAssignmentUserId);

        builder.Property(assignment => assignment.TimesheetLeaveAssignmentUserId)
            .HasColumnName("TimesheetLeaveAssignmentUserID")
            .ValueGeneratedOnAdd();

        builder.Property(assignment => assignment.UserId).HasColumnName("UserID");

        builder.Property(assignment => assignment.LeaveTypeId).HasColumnName("LeavetypeID");

        builder.Property(assignment => assignment.IsDelete).HasColumnName("IsDelete");

        builder.Property(assignment => assignment.CreatedDate)
            .HasColumnName("CreatedDate")
            .HasColumnType("datetime");

        builder.Property(assignment => assignment.CreatedBy).HasColumnName("CreatedBy");

        builder.Property(assignment => assignment.UpdatedDate)
            .HasColumnName("UpdatedDate")
            .HasColumnType("datetime");

        builder.Property(assignment => assignment.UpdatedBy).HasColumnName("UpdatedBy");

        builder.Property(assignment => assignment.DeletedDate)
            .HasColumnName("DeletedDate")
            .HasColumnType("datetime");

        builder.Property(assignment => assignment.DeletedBy).HasColumnName("DeletedBy");

        builder.Property(assignment => assignment.BalanceLeave)
            .HasColumnName("BalanceLeave")
            .HasColumnType("decimal(4,2)");

        // Soft delete: NULL and 0 are both live, so the test is "not true"
        // rather than "is false". "!= true" would drop the NULL rows, because
        // SQL's NULL <> 1 is unknown, not true.
        builder.HasQueryFilter(assignment => assignment.IsDelete == null || assignment.IsDelete == false);
    }
}
