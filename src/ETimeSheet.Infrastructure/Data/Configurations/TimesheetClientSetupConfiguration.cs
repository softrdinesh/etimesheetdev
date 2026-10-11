using ETimeSheet.Application.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ETimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Database mapping for <see cref="TimesheetClientSetup"/> onto the existing
/// <c>dbo.TimesheetClientSetup</c> table, per
/// <c>docs/database/schema/dbo.Timesheetclientsetup.sql</c>.
/// <para>
/// The column names mix conventions - <c>Clientname</c>, <c>Currencycode</c>,
/// <c>payment_terms_days</c>, <c>tax_identifier</c> - so every one is named
/// explicitly. No index is declared, because indexes are owned by the database.
/// </para>
/// </summary>
public class TimesheetClientSetupConfiguration : IEntityTypeConfiguration<TimesheetClientSetup>
{
    public void Configure(EntityTypeBuilder<TimesheetClientSetup> builder)
    {
        builder.ToTable("TimesheetClientSetup");

        // No primary key is recorded on the table yet; ClientID is IDENTITY,
        // which is what makes it usable as EF's key.
        builder.HasKey(client => client.ClientId);

        builder.Property(client => client.ClientId)
            .HasColumnName("ClientID")
            .ValueGeneratedOnAdd();

        builder.Property(client => client.ClientCode)
            .HasColumnName("ClientCode")
            .HasMaxLength(15)
            .IsRequired();

        builder.Property(client => client.ClientName)
            .HasColumnName("Clientname")
            .HasMaxLength(50)
            .IsRequired();

        // nvarchar(500) since 2026-10-12: it holds the uploaded logo's path,
        // a pre-signed URL of around 365 characters.
        builder.Property(client => client.Avatar)
            .HasColumnName("Avatar")
            .HasMaxLength(500);

        builder.Property(client => client.BillingEmail)
            .HasColumnName("BillingEmail")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(client => client.PhoneNumber)
            .HasColumnName("PhoneNumber")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(client => client.AlternatePhoneNumber)
            .HasColumnName("AlternatePhoneNumber")
            .HasMaxLength(150);

        builder.Property(client => client.AlternateEmail)
            .HasColumnName("AlternateEmail")
            .HasMaxLength(150);

        // nvarchar(max).
        builder.Property(client => client.BillingAddress).HasColumnName("BillingAddress");

        builder.Property(client => client.PaymentTermsDays).HasColumnName("payment_terms_days");

        builder.Property(client => client.CurrencyCode).HasColumnName("Currencycode");

        builder.Property(client => client.CountryId).HasColumnName("CountryID");

        builder.Property(client => client.TaxIdentifier)
            .HasColumnName("tax_identifier")
            .HasMaxLength(50);

        builder.Property(client => client.IsDeleted).HasColumnName("IsDeleted");

        builder.Property(client => client.DeleteDate)
            .HasColumnName("DeleteDate")
            .HasColumnType("datetime");

        // Soft delete: a deleted client is invisible to every query unless one
        // deliberately calls IgnoreQueryFilters().
        builder.HasQueryFilter(client => !client.IsDeleted);
    }
}
