using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="CountryCurrency"/> onto the existing
/// <c>dbo.CountryCurrency_Lookup</c> lookup table, per
/// <c>docs/database/schema/dbo.CountryCurrency_Lookup.sql</c>. No query filter - the
/// table has no soft-delete column.
/// </summary>
public class CountryCurrencyConfiguration : IEntityTypeConfiguration<CountryCurrency>
{
    public void Configure(EntityTypeBuilder<CountryCurrency> builder)
    {
        builder.ToTable("CountryCurrency_Lookup");

        builder.HasKey(currency => currency.CurrencyId);

        builder.Property(currency => currency.CurrencyId)
            .HasColumnName("CurrencyID")
            .ValueGeneratedOnAdd();

        builder.Property(currency => currency.CurrencySymbol)
            .HasColumnName("CurrencySymbol")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(currency => currency.CurrencyName)
            .HasColumnName("CurrencyName")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(currency => currency.CountryId).HasColumnName("CountryID");
    }
}
