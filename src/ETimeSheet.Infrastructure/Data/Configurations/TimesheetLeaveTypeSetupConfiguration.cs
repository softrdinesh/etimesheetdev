using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="TimesheetLeaveTypeSetup"/> onto the existing
/// <c>dbo.Timesheet_Leavetype_Setup</c> table, per
/// <c>docs/database/schema/dbo.Timesheet_Leavetype_Setup.sql</c>. Column names
/// are named explicitly, because <c>OrgID</c> and <c>NoofLeaveAllocated</c> do
/// not match their properties. No index is declared, because indexes are
/// owned by the database.
/// <para>
/// <c>LeaveTypeID</c> is the EF key. No primary key was reported on the table;
/// the column is <c>IDENTITY(1,1)</c>, so it is unique all the same.
/// </para>
/// </summary>
public class TimesheetLeaveTypeSetupConfiguration : IEntityTypeConfiguration<TimesheetLeaveTypeSetup>
{
    public void Configure(EntityTypeBuilder<TimesheetLeaveTypeSetup> builder)
    {
        builder.ToTable("Timesheet_Leavetype_Setup");

        builder.HasKey(leaveType => leaveType.LeaveTypeId);

        builder.Property(leaveType => leaveType.LeaveTypeId)
            .HasColumnName("LeaveTypeID")
            .ValueGeneratedOnAdd();

        builder.Property(leaveType => leaveType.LeaveType)
            .HasColumnName("LeaveType")
            .HasMaxLength(50);

        builder.Property(leaveType => leaveType.NoOfLeaveAllocated)
            .HasColumnName("NoofLeaveAllocated")
            .HasColumnType("decimal(4,2)");

        builder.Property(leaveType => leaveType.OrganizationId).HasColumnName("OrgID");

        builder.Property(leaveType => leaveType.CreateDate)
            .HasColumnName("CreateDate")
            .HasColumnType("datetime");

        builder.Property(leaveType => leaveType.CreatedBy).HasColumnName("CreatedBy");

        builder.Property(leaveType => leaveType.IsDelete).HasColumnName("IsDelete");

        builder.Property(leaveType => leaveType.UpdatedDate)
            .HasColumnName("UpdatedDate")
            .HasColumnType("datetime");

        builder.Property(leaveType => leaveType.UpdatedBy).HasColumnName("UpdatedBy");

        builder.Property(leaveType => leaveType.DeletedDate)
            .HasColumnName("DeletedDate")
            .HasColumnType("datetime");

        builder.Property(leaveType => leaveType.DeletedBy).HasColumnName("DeletedBy");

        // Soft delete: NULL and 0 are both live, so the test is "not true"
        // rather than "is false". "!= true" would drop the NULL rows, because
        // SQL's NULL <> 1 is unknown, not true.
        builder.HasQueryFilter(leaveType => leaveType.IsDelete == null || leaveType.IsDelete == false);
    }
}
