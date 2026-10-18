using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="TimesheetRoleAssignment"/> onto the existing
/// <c>dbo.Timesheet_role_assignment</c> table, per
/// <c>docs/database/schema/dbo.Timesheet_role_assignment.sql</c>. Column names
/// are irregularly cased and prefixed on the table, so each is named
/// explicitly. No index is declared, because indexes are owned by the database.
/// </summary>
public class TimesheetRoleAssignmentConfiguration : IEntityTypeConfiguration<TimesheetRoleAssignment>
{
    public void Configure(EntityTypeBuilder<TimesheetRoleAssignment> builder)
    {
        builder.ToTable("Timesheet_role_assignment");

        // No primary key is recorded on the table; AssignmentID is IDENTITY,
        // which is what makes it usable as EF's key.
        builder.HasKey(assignment => assignment.AssignmentId);

        builder.Property(assignment => assignment.AssignmentId)
            .HasColumnName("AssignmentID")
            .ValueGeneratedOnAdd();

        builder.Property(assignment => assignment.UserId).HasColumnName("UserID");

        builder.Property(assignment => assignment.IsClientSetup).HasColumnName("Mod_IsclientSetup");

        builder.Property(assignment => assignment.IsProject).HasColumnName("Mod_Isproject");

        builder.Property(assignment => assignment.IsProjectBudget).HasColumnName("Mod_IsProjectbudget");

        builder.Property(assignment => assignment.IsProjectCategory).HasColumnName("Mod_IsProjectCategory");

        builder.Property(assignment => assignment.IsStatusSetup).HasColumnName("Mod_IsStatusSetup");

        builder.Property(assignment => assignment.IsRoleSetup).HasColumnName("Mod_IsRoleSetup");

        builder.Property(assignment => assignment.IsUserSetup).HasColumnName("Mod_IsUsersetup");

        builder.Property(assignment => assignment.IsTimeLog).HasColumnName("Mod_IsTimeLog");

        builder.Property(assignment => assignment.CreateDate)
            .HasColumnName("CreateDate")
            .HasColumnType("datetime");

        builder.Property(assignment => assignment.CreatedBy).HasColumnName("CreatedBy");

        builder.Property(assignment => assignment.UpdatedDate)
            .HasColumnName("UpdatedDate")
            .HasColumnType("datetime");

        builder.Property(assignment => assignment.UpdatedBy).HasColumnName("Updatedby");

        // No query filter: the table has no soft-delete column.
    }
}
