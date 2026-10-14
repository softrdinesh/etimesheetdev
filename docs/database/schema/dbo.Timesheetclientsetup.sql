/*
    Table: dbo.Timesheetclientsetup
    Recorded: 2026-10-11 (sp_help column list supplied by the database owner,
              who created the table by hand)
    Revised:  2026-10-11 - ClientID is IDENTITY(1,1) (confirmed by the owner).
    Revised:  2026-10-12 - Avatar widened from varchar(255) to nvarchar(500)
              by the owner, to hold the uploaded logo's path.
    Revised:  2026-10-14 - by the owner: OrganizationID int NOT NULL added,
              the organisation the client belongs to.

    One row per client: billing details, contact numbers and emails, and the
    client's code, country, currency and organisation.

    Mapped by:
      src/ETimeSheet.Application/Models/Entities/TimesheetClientSetup.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/TimesheetClientSetupConfiguration.cs

    Read by:
      ../procedures/dbo.spc_GetClientListForMasterAdmin.sql
      (MasterAdminRepository, for GET api/v1/MasterAdmin/get-client-list/{orgID})

    Written by:
      ../procedures/dbo.spc_AddEditClientSetup.sql
      (MasterAdminRepository, for POST api/v1/MasterAdmin/add-or-edit-client-setup)

    Column quirks worth knowing:
      - The table name is exactly "Timesheetclientsetup" - lower-case "client"
        and "setup", unlike dbo.TimesheetMasterSetup.
      - Column names mix conventions: PascalCase (BillingEmail, ClientCode),
        an irregular lower case (Clientname, Currencycode) and snake_case
        (payment_terms_days, tax_identifier). Map each one by its exact name.
      - Currencycode holds dbo.CountryCurrency.CurrencyID, despite its name
        - not an ISO code. It is never sent by the API caller: since
        2026-10-11 spc_AddEditClientSetup derives it from the client's
        CountryID. Rows saved before then may hold a value that is not a
        CurrencyID. There is no foreign key.
      - CountryID is presumably dbo.Country.ID; no foreign key was reported.
      - BillingAddress is nvarchar(max).
      - Avatar is nvarchar(500) (widened from varchar(255) by the owner): it
        holds the client logo's path as the document service returns it, a
        pre-signed S3 URL of around 365 characters that EXPIRES after 48 hours.
      - DeleteDate is plain datetime - local or UTC is not stated.
      - IsDeleted is NOT NULL; no default was reported.
      - OrganizationID is the organisation the client belongs to - presumably
        dbo.Organization's id; no foreign key was reported. Taken from the
        add/edit payload. Recorded as the last column, where
        ALTER TABLE ... ADD puts it. No default was reported.

    STILL UNCONFIRMED:
      - Whether ClientID is the primary key. It is IDENTITY(1,1), but no key
        was reported, so none is recorded below.
      - Defaults, constraints and indexes - none were supplied.

    Lengths below are characters. sp_help reports nvarchar lengths in BYTES, so
    the 100 / 300 / 30 it prints are 50 / 150 / 15 characters.
*/

IF OBJECT_ID(N'dbo.Timesheetclientsetup', N'U') IS NOT NULL
    DROP TABLE dbo.Timesheetclientsetup;
GO

CREATE TABLE dbo.TimesheetClientSetup
(
    ClientID              int             IDENTITY(1,1) NOT NULL,
    ClientCode            nvarchar(15)    NOT NULL,
    Clientname            nvarchar(50)    NOT NULL,
		Avatar                nvarchar(500)    NULL,
    BillingEmail          nvarchar(150)   NOT NULL,
		PhoneNumber           nvarchar(150)   NOT NULL,
    AlternatePhoneNumber  nvarchar(150)   NULL,
    AlternateEmail        nvarchar(150)   NULL,
    BillingAddress        nvarchar(max)   NULL,
    payment_terms_days    int             NOT NULL,
    Currencycode          int             NOT NULL,
    CountryID             int             NULL,
    tax_identifier        nvarchar(50)    NULL,
    IsDeleted             bit             NOT NULL,
    DeleteDate            datetime        NULL,
    OrganizationID        int             NOT NULL
);
GO