using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="TimesheetUserRole"/> onto the existing
/// <c>dbo.TimesheetUserRole</c> table, per
/// <c>docs/database/schema/dbo.TimesheetUserRole.sql</c>. Column names are
/// irregularly cased on the table, so each is named explicitly. No index is
/// declared, because indexes are owned by the database.
/// </summary>
public class TimesheetUserRoleConfiguration : IEntityTypeConfiguration<TimesheetUserRole>
{
    public void Configure(EntityTypeBuilder<TimesheetUserRole> builder)
    {
        builder.ToTable("TimesheetUserRole");

        // No primary key is recorded on the table; RoleID is IDENTITY, which
        // is what makes it usable as EF's key.
        builder.HasKey(role => role.RoleId);

        builder.Property(role => role.RoleId)
            .HasColumnName("RoleID")
            .ValueGeneratedOnAdd();

        builder.Property(role => role.RoleName)
            .HasColumnName("Rolename")
            .HasMaxLength(50);

        builder.Property(role => role.OrganizationId).HasColumnName("OrganizationID");

        builder.Property(role => role.CreateDate)
            .HasColumnName("CreateDate")
            .HasColumnType("datetime");

        builder.Property(role => role.CreatedBy).HasColumnName("Createdby");

        builder.Property(role => role.IsDelete).HasColumnName("IsDelete");

        builder.Property(role => role.DeletedDate)
            .HasColumnName("Deleteddate")
            .HasColumnType("datetime");

        builder.Property(role => role.DeletedBy).HasColumnName("Deletedby");

        // Soft delete: NULL and 0 are both live, so the test is "not true"
        // rather than "is false". "!= true" would drop the NULL rows, because
        // SQL's NULL <> 1 is unknown, not true.
        builder.HasQueryFilter(role => role.IsDelete == null || role.IsDelete == false);
    }
}
