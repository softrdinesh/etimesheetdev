using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="TimesheetProject"/> onto the existing
/// <c>dbo.TimesheetProject</c> table, per
/// <c>docs/database/schema/dbo.TimesheetProject.sql</c>. Column names are
/// irregularly cased on the table, so each is named explicitly. No index is
/// declared, because indexes are owned by the database.
/// </summary>
public class TimesheetProjectConfiguration : IEntityTypeConfiguration<TimesheetProject>
{
    public void Configure(EntityTypeBuilder<TimesheetProject> builder)
    {
        builder.ToTable("TimesheetProject");

        // No primary key is recorded on the table; ProjectID is IDENTITY,
        // which is what makes it usable as EF's key.
        builder.HasKey(project => project.ProjectId);

        builder.Property(project => project.ProjectId)
            .HasColumnName("ProjectID")
            .ValueGeneratedOnAdd();

        builder.Property(project => project.Code)
            .HasColumnName("Code")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(project => project.ProjectName)
            .HasColumnName("Projectname")
            .HasMaxLength(100);

        builder.Property(project => project.TotalBudget)
            .HasColumnName("TotalBudget")
            .HasColumnType("decimal(18,2)");

        builder.Property(project => project.TotalHours)
            .HasColumnName("Totalhours")
            .HasColumnType("decimal(18,2)");

        builder.Property(project => project.StartDate)
            .HasColumnName("StartDate")
            .HasColumnType("datetime");

        builder.Property(project => project.EndDate)
            .HasColumnName("EndDate")
            .HasColumnType("datetime");

        builder.Property(project => project.CurrencyCode).HasColumnName("CurrencyCode");

        builder.Property(project => project.OrganizationId).HasColumnName("OrganizationID");

        builder.Property(project => project.Status).HasColumnName("Status");

        builder.Property(project => project.ClientId).HasColumnName("ClientID");
    }
}
