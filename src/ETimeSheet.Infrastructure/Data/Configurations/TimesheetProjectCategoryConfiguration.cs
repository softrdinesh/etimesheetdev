using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="TimesheetProjectCategory"/> onto the existing
/// <c>dbo.TimesheetProjectCategory</c> table, per
/// <c>docs/database/schema/dbo.TimesheetProjectCategory.sql</c>. Column names
/// are irregularly cased on the table, so each is named explicitly. No index is
/// declared, because indexes are owned by the database.
/// </summary>
public class TimesheetProjectCategoryConfiguration : IEntityTypeConfiguration<TimesheetProjectCategory>
{
    public void Configure(EntityTypeBuilder<TimesheetProjectCategory> builder)
    {
        builder.ToTable("TimesheetProjectCategory");

        // No primary key is recorded on the table; ID is IDENTITY, which is
        // what makes it usable as EF's key.
        builder.HasKey(category => category.CategoryId);

        builder.Property(category => category.CategoryId)
            .HasColumnName("ID")
            .ValueGeneratedOnAdd();

        builder.Property(category => category.CategoryName)
            .HasColumnName("Categoryname")
            .HasMaxLength(50);

        builder.Property(category => category.OrganizationId).HasColumnName("OrganizationID");

        builder.Property(category => category.IsDelete).HasColumnName("IsDelete");

        builder.Property(category => category.CreateDate)
            .HasColumnName("CreateDate")
            .HasColumnType("datetime");

        builder.Property(category => category.CreatedBy).HasColumnName("CreatedBy");

        builder.Property(category => category.DeletedDate)
            .HasColumnName("DeletedDate")
            .HasColumnType("datetime");

        builder.Property(category => category.DeletedBy).HasColumnName("Deletedby");

        // Soft delete: NULL and 0 are both live, so the test is "not true"
        // rather than "is false". "!= true" would drop the NULL rows, because
        // SQL's NULL <> 1 is unknown, not true.
        builder.HasQueryFilter(category => category.IsDelete == null || category.IsDelete == false);
    }
}
