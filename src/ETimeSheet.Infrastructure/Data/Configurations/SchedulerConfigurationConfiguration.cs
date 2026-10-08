using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="SchedulerConfiguration"/> onto the existing
/// <c>dbo.SchedulerConfiguration</c> table.
/// <para>
/// This is a database-first mapping: the column names and types below must
/// match <c>docs/database/schema/dbo.SchedulerConfiguration.sql</c> exactly.
/// No index or constraint is declared, because those are owned by the database.
/// </para>
/// </summary>
public class SchedulerConfigurationConfiguration : IEntityTypeConfiguration<SchedulerConfiguration>
{
    public void Configure(EntityTypeBuilder<SchedulerConfiguration> builder)
    {
        builder.ToTable("SchedulerConfiguration");

        builder.HasKey(configuration => configuration.SchedulerConfigurationId);

        builder.Property(configuration => configuration.SchedulerConfigurationId)
            .HasColumnName("SchedulerConfigurationID")
            .ValueGeneratedOnAdd();

        builder.Property(configuration => configuration.SchedulerName)
            .HasColumnName("SchedulerName")
            .HasMaxLength(150)
            .IsUnicode(false);

        builder.Property(configuration => configuration.IsEnabled).HasColumnName("IsEnabled");

        // tinyint: the enum's underlying type is byte, so no converter is needed.
        builder.Property(configuration => configuration.ScheduleTypeId)
            .HasColumnName("ScheduleTypeID")
            .HasColumnType("tinyint");

        builder.Property(configuration => configuration.ScheduleDateTime)
            .HasColumnName("ScheduleDateTime")
            .HasColumnType("datetime2(0)");

        builder.Property(configuration => configuration.IsRepeatEnabled).HasColumnName("IsRepeatEnabled");

        builder.Property(configuration => configuration.RepeatInterval).HasColumnName("RepeatInterval");

        builder.Property(configuration => configuration.RepeatIntervalType)
            .HasColumnName("RepeatIntervalType")
            .HasColumnType("char(1)")
            .HasMaxLength(1)
            .IsFixedLength()
            .IsUnicode(false);

        builder.Property(configuration => configuration.CreatedDate)
            .HasColumnName("CreatedDate")
            .HasColumnType("datetime2(0)");
    }
}
