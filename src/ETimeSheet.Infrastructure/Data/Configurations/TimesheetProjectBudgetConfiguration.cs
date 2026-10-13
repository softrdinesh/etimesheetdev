using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="TimesheetProjectBudget"/> onto the existing
/// <c>dbo.TimesheetProjectBudget</c> table, per
/// <c>docs/database/schema/dbo.TimesheetProjectBudget.sql</c>. Column names
/// mix conventions on the table, so each is named explicitly. No index is
/// declared, because indexes are owned by the database.
/// </summary>
public class TimesheetProjectBudgetConfiguration : IEntityTypeConfiguration<TimesheetProjectBudget>
{
    public void Configure(EntityTypeBuilder<TimesheetProjectBudget> builder)
    {
        builder.ToTable("TimesheetProjectBudget");

        // No primary key is recorded on the table; BudgetID is IDENTITY,
        // which is what makes it usable as EF's key.
        builder.HasKey(budget => budget.BudgetId);

        builder.Property(budget => budget.BudgetId)
            .HasColumnName("BudgetID")
            .ValueGeneratedOnAdd();

        builder.Property(budget => budget.ProjectId).HasColumnName("ProjectID");

        builder.Property(budget => budget.ProjectCode)
            .HasColumnName("ProjectCode")
            .HasMaxLength(50);

        builder.Property(budget => budget.ProjectCategoryId).HasColumnName("ProjectCategoryID");

        builder.Property(budget => budget.AllocatedHours)
            .HasColumnName("AllocatedHours")
            .HasColumnType("decimal(18,2)");

        builder.Property(budget => budget.HourlyRate)
            .HasColumnName("Hourlyrate")
            .HasColumnType("decimal(18,2)");

        builder.Property(budget => budget.AllocatedAmount)
            .HasColumnName("Allocated_amount")
            .HasColumnType("decimal(18,2)");

        builder.Property(budget => budget.PeriodStart)
            .HasColumnName("PeriodStart")
            .HasColumnType("date");

        builder.Property(budget => budget.PeriodEnd)
            .HasColumnName("PeriodEnd")
            .HasColumnType("date");
    }
}
