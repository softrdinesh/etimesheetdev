/*
    Procedure: dbo.spc_AddEditClientSetup
    Recorded:  2026-10-11 (applied by the database owner)
    Revised:   2026-10-11 - DRAFT, NOT YET RE-APPLIED: @PCurrencyCode removed;
               the currency is derived from @PCountryID via
               dbo.CountryCurrency, and @PCountryID is now required.
    Revised:   2026-10-14 - DRAFT: @POrgID added (required) and written to
               OrganizationID on add and edit; OrganizationID returned.

    Adds a client to dbo.TimesheetClientSetup, or edits one, and returns the
    saved row. Called by POST api/v1/MasterAdmin/add-or-edit-client-setup.

    Parameters:
      @PClientID   NULL or 0 adds a new client; any other value edits that
                   client.
      @PCountryID  required: the client's currency is derived from it.
      @POrgID      required: the organisation the client belongs to, written
                   to OrganizationID (NOT NULL) on add and on edit.
      the rest     the editable columns. ClientCode, Currencycode, IsDeleted
                   and DeleteDate are never taken as parameters.

    Currency (add and edit alike):
      Currencycode is set to the CurrencyID of the client's country in
      dbo.CountryCurrency - the lowest one, should a country ever have more
      than one row. Re-derived on every edit, so changing a client's
      country changes its currency too. A country with no currency row
      (Antarctica) raises error 50002 and nothing is saved; the API checks
      for this first and answers 400, so the error is a backstop.

    Add (@PClientID NULL or 0):
      The client gets the next code in the sequence CL0001, CL0002 ...
      CL9999, CL10000 ... - the number keeps counting, zero-padded to at
      least four digits. "Next" follows the highest existing code of the
      form CL + digits across EVERY row, deleted ones included, because a
      deleted client has still spent its code.

      The latest code is read WITH (UPDLOCK, HOLDLOCK) inside the insert's
      transaction, so two adds at the same moment cannot both pick the same
      code: the second waits for the first to commit, then reads its code.
      A code longer than the column's 15 characters raises error 50001
      rather than being truncated into a duplicate.

    Edit (@PClientID > 0):
      Updates the client only if it is not deleted (IsDeleted = 0). Its
      ClientCode never changes.

    Result:
      The saved client - the same columns, with the same aliases, as
      dbo.spc_GetClientListForMasterAdmin, currency name and symbol included. NO ROW when an edit names a
      client that does not exist or is deleted; the API answers 404 for
      that.

    The tables are recorded at:
      ../schema/dbo.Timesheetclientsetup.sql
      ../schema/dbo.CountryCurrency.sql
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_AddEditClientSetup]
    @PClientID              INT            = NULL,
    @PClientName            NVARCHAR(50),
    @PAvatar                VARCHAR(500)   = NULL,
    @PBillingEmail          NVARCHAR(150),
    @PPhoneNumber           NVARCHAR(150),
    @PAlternatePhoneNumber  NVARCHAR(150)  = NULL,
    @PAlternateEmail        NVARCHAR(150)  = NULL,
    @PBillingAddress        NVARCHAR(MAX)  = NULL,
    @PPaymentTermsDays      INT,
    @PCountryID             INT,
    @PTaxIdentifier         NVARCHAR(50)   = NULL,
    @POrgID                 INT
AS
BEGIN
    SET NOCOUNT ON;

    -- Any error rolls the whole transaction back, so a failed add never
    -- leaves the table locked or half-written.
    SET XACT_ABORT ON;

    DECLARE @ClientID INT;

    -- =========================================================
    -- The client's currency, from its country
    -- =========================================================
    DECLARE @CurrencyID INT =
    (
        SELECT TOP (1) cc.CurrencyID
        FROM dbo.CountryCurrency cc
        WHERE cc.CountryID = @PCountryID
        ORDER BY cc.CurrencyID
    );

    IF @CurrencyID IS NULL
        THROW 50002, 'The client''s country has no currency in dbo.CountryCurrency.', 1;

    IF ISNULL(@PClientID, 0) = 0
    BEGIN
        BEGIN TRANSACTION;

        -- =========================================================
        -- The highest generated code, held locked until COMMIT
        -- =========================================================
        -- Longest first, then greatest: CL10000 outranks CL9999, which a
        -- plain text comparison would get backwards. Only CL + digits counts.
        DECLARE @LatestCode NVARCHAR(15) =
        (
            SELECT TOP (1) tcs.ClientCode
            FROM dbo.TimesheetClientSetup tcs WITH (UPDLOCK, HOLDLOCK)
            WHERE tcs.ClientCode LIKE 'CL[0-9]%'
              AND SUBSTRING(tcs.ClientCode, 3, 13) NOT LIKE '%[^0-9]%'
            ORDER BY LEN(tcs.ClientCode) DESC, tcs.ClientCode DESC
        );

        DECLARE @NextNumber BIGINT =
            ISNULL(TRY_CAST(SUBSTRING(@LatestCode, 3, 13) AS BIGINT), 0) + 1;

        DECLARE @NextDigits VARCHAR(20) = CAST(@NextNumber AS VARCHAR(20));

        DECLARE @NextCode VARCHAR(20) = 'CL' +
            CASE WHEN LEN(@NextDigits) < 4
                 THEN RIGHT('0000' + @NextDigits, 4)
                 ELSE @NextDigits
            END;

        IF LEN(@NextCode) > 15
            THROW 50001, 'The client code sequence has run out of room: ClientCode holds at most 15 characters.', 1;

        INSERT INTO dbo.TimesheetClientSetup
        (
            ClientCode, Clientname, Avatar, BillingEmail, PhoneNumber,
            AlternatePhoneNumber, AlternateEmail, BillingAddress,
            payment_terms_days, Currencycode, CountryID, tax_identifier,
            IsDeleted, DeleteDate, OrganizationID
        )
        VALUES
        (
            @NextCode, @PClientName, @PAvatar, @PBillingEmail, @PPhoneNumber,
            @PAlternatePhoneNumber, @PAlternateEmail, @PBillingAddress,
            @PPaymentTermsDays, @CurrencyID, @PCountryID, @PTaxIdentifier,
            0, NULL, @POrgID
        );

        SET @ClientID = CAST(SCOPE_IDENTITY() AS INT);

        COMMIT TRANSACTION;
    END
    ELSE
    BEGIN
        -- A deleted client is not edited back to life: no row matches, and
        -- the SELECT below returns nothing.
        UPDATE dbo.TimesheetClientSetup
        SET Clientname            = @PClientName,
            Avatar                = @PAvatar,
            BillingEmail          = @PBillingEmail,
            PhoneNumber           = @PPhoneNumber,
            AlternatePhoneNumber  = @PAlternatePhoneNumber,
            AlternateEmail        = @PAlternateEmail,
            BillingAddress        = @PBillingAddress,
            payment_terms_days    = @PPaymentTermsDays,
            Currencycode          = @CurrencyID,
            CountryID             = @PCountryID,
            tax_identifier        = @PTaxIdentifier,
            OrganizationID        = @POrgID
        WHERE ClientID = @PClientID
          AND IsDeleted = 0;

        SET @ClientID = @PClientID;
    END

    -- =========================================================
    -- The saved row - same columns as spc_GetClientListForMasterAdmin
    -- =========================================================
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
    WHERE tcs.ClientID = @ClientID
      AND tcs.IsDeleted = 0;
END
GO
