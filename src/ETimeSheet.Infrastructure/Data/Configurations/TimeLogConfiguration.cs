using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="TimeLog"/> onto the existing <c>TimeLog</c>
/// table. All schema decisions - table name, column names, types, lengths,
/// filters - live here rather than as attributes on the entity, keeping the
/// entity free of persistence concerns.
/// <para>
/// This is a database-first mapping: the column types below describe a table
/// that already exists and must match it exactly. No index is declared, because
/// indexes are owned by the database, and EF Core only ever uses them to
/// generate migrations - never to plan a query.
/// </para>
/// </summary>
public class TimeLogConfiguration : IEntityTypeConfiguration<TimeLog>
{
    public void Configure(EntityTypeBuilder<TimeLog> builder)
    {
        builder.ToTable("TimeLog");

        builder.HasKey(timeLog => timeLog.SheetId);

        builder.Property(timeLog => timeLog.SheetId)
            .HasColumnName("SheetID")
            .ValueGeneratedOnAdd();

        // varchar, not nvarchar: IsUnicode(false) is what stops EF Core sending
        // an N-prefixed parameter, which would force a conversion on the column
        // and discard any index on it.
        builder.Property(timeLog => timeLog.SheetCode)
            .HasColumnName("SheetCode")
            .HasColumnType("varchar(15)")
            .IsUnicode(false)
            .HasMaxLength(15);

        builder.Property(timeLog => timeLog.TaskId)
            .HasColumnName("TaskID");

        // nvarchar(max) - no length, so no HasMaxLength.
        builder.Property(timeLog => timeLog.Description)
            .HasColumnName("Description")
            .HasColumnType("nvarchar(max)");

        builder.Property(timeLog => timeLog.StartTime)
            .HasColumnName("StartTime")
            .HasColumnType("time(7)");

        builder.Property(timeLog => timeLog.EndTime)
            .HasColumnName("EndTime")
            .HasColumnType("time(7)");

        builder.Property(timeLog => timeLog.StartDate)
            .HasColumnName("StartDate")
            .HasColumnType("date");

        builder.Property(timeLog => timeLog.EndDate)
            .HasColumnName("EndDate")
            .HasColumnType("date");

        builder.Property(timeLog => timeLog.UserId)
            .HasColumnName("UserID");

        // Stored as its underlying int so the column stays stable if the enum
        // member is ever renamed. 1 = Save, 2 = Draft only - see
        // Constants.TimeLog.Status.
        builder.Property(timeLog => timeLog.Status)
            .HasColumnName("Status")
            .HasConversion<int?>();

        // Audit columns, named as the existing schema names them.
        builder.Property(timeLog => timeLog.CreatedBy)
            .HasColumnName("CreatedBy");

        builder.Property(timeLog => timeLog.CreateDate)
            .HasColumnName("CreateDate")
            .HasColumnType("datetime");

        builder.Property(timeLog => timeLog.UpdatedBy)
            .HasColumnName("UpdatedBy");

        builder.Property(timeLog => timeLog.UpdateDate)
            .HasColumnName("UpdateDate")
            .HasColumnType("datetime");

        builder.Property(timeLog => timeLog.IsDeleted)
            .HasColumnName("IsDeleted")
            .IsRequired();

        builder.Property(timeLog => timeLog.DeletedBy)
            .HasColumnName("DeletedBy");

        builder.Property(timeLog => timeLog.DeleteDate)
            .HasColumnName("DeleteDate")
            .HasColumnType("datetime");

        // Soft-deleted rows are invisible to every query in the application, so
        // no repository method has to remember to filter them out. Expressed as
        // "<> 1" rather than "== 0" so that it matches the stored procedures
        // exactly: any stray value other than 1 counts as not deleted in both.
        // Use IgnoreQueryFilters() in the rare audit query that needs them.
        builder.HasQueryFilter(timeLog => timeLog.IsDeleted != 1);
    }
}
