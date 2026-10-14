using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="TimesheetProjectStatus"/> onto the existing
/// <c>dbo.TimesheetProjectStatus</c> table, per
/// <c>docs/database/schema/dbo.TimesheetProjectStatus.sql</c>. Column names
/// are irregularly cased on the table, so each is named explicitly. No index is
/// declared, because indexes are owned by the database.
/// </summary>
public class TimesheetProjectStatusConfiguration : IEntityTypeConfiguration<TimesheetProjectStatus>
{
    public void Configure(EntityTypeBuilder<TimesheetProjectStatus> builder)
    {
        builder.ToTable("TimesheetProjectStatus");

        // No primary key is recorded on the table; StatusID is IDENTITY, which
        // is what makes it usable as EF's key.
        builder.HasKey(status => status.StatusId);

        builder.Property(status => status.StatusId)
            .HasColumnName("StatusID")
            .ValueGeneratedOnAdd();

        builder.Property(status => status.Status)
            .HasColumnName("Status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(status => status.ColorCode)
            .HasColumnName("Colorcode")
            .HasMaxLength(25);

        builder.Property(status => status.CreateDate)
            .HasColumnName("CreateDate")
            .HasColumnType("datetime");

        builder.Property(status => status.CreatedBy).HasColumnName("CreatedBy");

        builder.Property(status => status.DeletedDate)
            .HasColumnName("DeletedDate")
            .HasColumnType("datetime");

        builder.Property(status => status.DeletedBy).HasColumnName("Deletedby");

        builder.Property(status => status.UpdatedDate)
            .HasColumnName("UpdatedDate")
            .HasColumnType("datetime");

        builder.Property(status => status.UpdatedBy).HasColumnName("Updatedby");

        builder.Property(status => status.OrganizationId).HasColumnName("OrgID");

        // Soft delete: there is no flag column, so a row is live while
        // DeletedDate is null.
        builder.HasQueryFilter(status => status.DeletedDate == null);
    }
}
