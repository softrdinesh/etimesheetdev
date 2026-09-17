using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="TimesheetMasterSetup"/> onto the existing
/// <c>dbo.TimesheetMasterSetup</c> table.
/// <para>
/// This is a database-first mapping: the column names and types below describe
/// a table that already exists and must match it exactly. No index is declared,
/// because indexes are owned by the database.
/// </para>
/// </summary>
public class TimesheetMasterSetupConfiguration : IEntityTypeConfiguration<TimesheetMasterSetup>
{
    public void Configure(EntityTypeBuilder<TimesheetMasterSetup> builder)
    {
        builder.ToTable("TimesheetMasterSetup");

        builder.HasKey(setup => setup.SetupId);

        builder.Property(setup => setup.SetupId)
            .HasColumnName("SetupID")
            .ValueGeneratedOnAdd();

        // time(7), not a number - see the entity.
        builder.Property(setup => setup.MaxTimeInHrs)
            .HasColumnName("MaxTimeinhrs")
            .HasColumnType("time(7)");

        builder.Property(setup => setup.MaxTimInMins)
            .HasColumnName("MaxTiminmins")
            .HasColumnType("time(7)");

        builder.Property(setup => setup.UserId).HasColumnName("UserID");
        builder.Property(setup => setup.OrganizationId).HasColumnName("OrganizationID");
        builder.Property(setup => setup.ContractType).HasColumnName("ContractType");
        builder.Property(setup => setup.CountryId).HasColumnName("CountryID");

        // Day columns: plain int since 2026-09-17, holding dbo.DayMaster.DayID.
        // No HasOne/WithMany to DayMaster and no foreign key is declared,
        // because the database declares none - modelling a relationship the
        // database does not enforce would have EF Core generate joins and
        // fixup for a constraint that can be violated by any other writer.
        builder.Property(setup => setup.StartDay).HasColumnName("StartDay");
        builder.Property(setup => setup.EndDay).HasColumnName("EndDay");

        // Still the database's one-word, lower-case-d spelling.
        builder.Property(setup => setup.ExceptionDay).HasColumnName("Exceptionday");

        builder.Property(setup => setup.TimeEntryLockAt)
            .HasColumnName("TimeEntryLockAt")
            .HasColumnType("time(7)");

        // CanUserLoggedPreDayTime is NOT mapped here on purpose - it is not a
        // column on this table. See the note on the entity.

        builder.Property(setup => setup.IsDelete).HasColumnName("IsDelete");
        builder.Property(setup => setup.CreateDate).HasColumnName("CreateDate").HasColumnType("datetime");
        builder.Property(setup => setup.CreatedBy).HasColumnName("CreatedBy");
        builder.Property(setup => setup.UpdateDate).HasColumnName("UpdateDate").HasColumnType("datetime");
        builder.Property(setup => setup.UpdatedBy).HasColumnName("UpdatedBy");
        builder.Property(setup => setup.DeleteDate).HasColumnName("DeleteDate").HasColumnType("datetime");

        // The database spells this one with a lower-case "b".
        builder.Property(setup => setup.DeletedBy).HasColumnName("Deletedby");

        // Soft-deleted rows are hidden from every query against this entity.
        // Written to treat NULL as live: "IsDelete != true" alone would compare
        // NULL <> 1, which is UNKNOWN in SQL and would silently drop every row
        // that has never been touched by a delete.
        builder.HasQueryFilter(setup => setup.IsDelete == null || setup.IsDelete == false);
    }
}
