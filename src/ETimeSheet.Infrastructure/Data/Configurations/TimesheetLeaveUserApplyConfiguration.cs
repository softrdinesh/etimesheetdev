using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="TimesheetLeaveUserApply"/> onto the existing
/// <c>dbo.Timesheet_Leave_User_apply</c> table, per
/// <c>docs/database/schema/dbo.Timesheet_Leave_User_apply.sql</c>. Column names
/// are named explicitly, because <c>OrgID</c>, <c>NoofDays</c> and
/// <c>LeavetypeID</c> do not match their properties. No query filter: the table has no
/// soft-delete column. No index is declared, because indexes are owned by the
/// database.
/// </summary>
public class TimesheetLeaveUserApplyConfiguration : IEntityTypeConfiguration<TimesheetLeaveUserApply>
{
    public void Configure(EntityTypeBuilder<TimesheetLeaveUserApply> builder)
    {
        builder.ToTable("Timesheet_Leave_User_apply");

        builder.HasKey(application => application.TimesheetLeaveUserApplyId);

        builder.Property(application => application.TimesheetLeaveUserApplyId)
            .HasColumnName("TimesheetLeaveUserApplyID")
            .ValueGeneratedOnAdd();

        builder.Property(application => application.LeaveId).HasColumnName("LeaveID");

        builder.Property(application => application.UserId).HasColumnName("UserID");

        builder.Property(application => application.OrganizationId).HasColumnName("OrgID");

        builder.Property(application => application.StartDate)
            .HasColumnName("StartDate")
            .HasColumnType("datetime");

        builder.Property(application => application.EndDate)
            .HasColumnName("EndDate")
            .HasColumnType("datetime");

        builder.Property(application => application.NoOfDays)
            .HasColumnName("NoofDays")
            .HasColumnType("decimal(4,2)");

        builder.Property(application => application.LeaveTypeId).HasColumnName("LeavetypeID");

        builder.Property(application => application.CreatedDate)
            .HasColumnName("CreatedDate")
            .HasColumnType("datetime");

        builder.Property(application => application.CreatedBy).HasColumnName("CreatedBy");

        builder.Property(application => application.ApprovedDate)
            .HasColumnName("ApprovedDate")
            .HasColumnType("datetime");

        builder.Property(application => application.ApprovedBy).HasColumnName("ApprovedBy");

        builder.Property(application => application.RejectedDate)
            .HasColumnName("RejectedDate")
            .HasColumnType("datetime");

        builder.Property(application => application.RejectedBy).HasColumnName("RejectedBy");

        builder.Property(application => application.UserComments).HasColumnName("UserComments");

        builder.Property(application => application.ApproverComments).HasColumnName("ApproverComments");

        builder.Property(application => application.LeaveStatus).HasColumnName("LeaveStatus");
    }
}
