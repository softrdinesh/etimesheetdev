/*
    Procedure: dbo.spc_GetClientListForMasterAdmin
    Recorded:  2026-10-11 (applied by the database owner)
    Revised:   2026-10-11 - DRAFT, NOT YET RE-APPLIED: CurrencyCode is now
               returned as CurrencyID, with CurrencyName and CurrencySymbol
               joined from dbo.CountryCurrency.
    Revised:   2026-10-14 - DRAFT: OrganizationID added to the result.
    Revised:   2026-10-14 - DRAFT, NOT YET APPLIED: @POrgID added; only that
               organisation's clients are returned.

    Returns the live clients of one organisation in dbo.TimesheetClientSetup,
    for the Master Admin client list
    (GET api/v1/MasterAdmin/get-client-list/{orgID}).

    Parameters:
      @POrgID  the organisation (dbo.TimesheetClientSetup.OrganizationID).

    Clients:
      Only rows with IsDeleted = 0. A deleted client (IsDeleted = 1) is never
      returned, and IsDeleted / DeleteDate are not in the result.

    Result columns:
      Aliased to one consistent PascalCase spelling, because the table mixes
      conventions: Clientname -> ClientName, payment_terms_days ->
      PaymentTermsDays, tax_identifier -> TaxIdentifier, OrganizationID. Currencycode comes
      back as CurrencyID, because that is what it holds:
      dbo.CountryCurrency.CurrencyID.
      The aliases are what an EF Core result mapping binds to.

    Order:
      By ClientName, then ClientID so two clients with the same name always
      come back in the same order.

    Result:
      An organisation with no clients gets an empty result, not an error.

    The tables are recorded at:
      ../schema/dbo.Timesheetclientsetup.sql
      ../schema/dbo.CountryCurrency.sql
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_GetClientListForMasterAdmin]
    @POrgID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        tcs.ClientID              AS ClientID,
        tcs.ClientCode            AS ClientCode,
        tcs.Clientname            AS ClientName,
        tcs.Avatar                AS Avatar,
        tcs.BillingEmail          AS BillingEmail,
        tcs.PhoneNumber           AS PhoneNumber,
        tcs.AlternatePhoneNumber  AS AlternatePhoneNumber,
        tcs.AlternateEmail        AS AlternateEmail,
        tcs.BillingAddress        AS BillingAddress,
        tcs.payment_terms_days    AS PaymentTermsDays,
        tcs.Currencycode          AS CurrencyID,
        cc.CurrencyName           AS CurrencyName,
        cc.CurrencySymbol         AS CurrencySymbol,
        tcs.CountryID             AS CountryID,
        tcs.tax_identifier        AS TaxIdentifier,
        tcs.OrganizationID        AS OrganizationID
    FROM dbo.TimesheetClientSetup tcs
    -- LEFT: a client saved before currencies were derived may hold a
    -- Currencycode with no CountryCurrency row; it is still listed, with a
    -- null name and symbol.
    LEFT JOIN dbo.CountryCurrency cc ON cc.CurrencyID = tcs.Currencycode
    WHERE tcs.OrganizationID = @POrgID
      AND tcs.IsDeleted = 0
    ORDER BY tcs.Clientname, tcs.ClientID;
END
GO
