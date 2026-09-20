/*
    Table: dbo.Country
    Recorded: 2026-09-20 (column list supplied by the database owner)

    The country lookup. Read-only as far as this API is concerned: the rows are
    maintained by hand in SQL Server, and nothing in the application inserts,
    updates or deletes one.

    Mapped by:
      src/ETimeSheet.Application/Models/Entities/Country.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/CountryConfiguration.cs

    Read by:
      src/ETimeSheet.Infrastructure/Repositories/CountryRepository.cs
      (AdminService, to resolve a timesheet setup's TimeZone)

    Column quirks worth knowing:
      - TimeZone holds ONE IANA zone id, or SEVERAL separated by commas with no
        spaces, when the country spans more than one:

            GB  Europe/London
            CN  Asia/Shanghai,Asia/Urumqi
            US  America/New_York,America/Detroit, ... (29 zones, 598 characters)

        The first id is the country's primary/most-populous zone. The count of
        that list is what the save API keys on: one zone is used outright, and
        several means the caller must choose one of them.

        It was populated from the IANA tzdata zone.tab by
        docs/database/data/dbo.Country_UpdateTimeZone.sql, matching on Code.
        That script also widens the column to 1000 if it is narrower, because
        the US value alone needs 598.

      - CreateDate is datetimeoffset(7), NOT the plain datetime used by
        dbo.TimeLog and dbo.TimesheetMasterSetup. Map it as DateTimeOffset or
        the offset is silently discarded.

      - Name, Code and TimeZone are all NULLABLE. A country row with no TimeZone
        is therefore possible, and the save API answers 400 for it rather than
        storing a setup with no zone.

    STILL UNCONFIRMED:
      - Whether ID is an IDENTITY column, and whether the primary key is
        clustered. Nothing in the API inserts into this table, so neither
        affects it today.

    Lengths below are characters. sp_help reports nvarchar lengths in BYTES, so
    the 160 / 12 / 2000 it prints for Name / Code / TimeZone are 80 / 6 / 1000
    characters.
*/

IF OBJECT_ID(N'dbo.Country', N'U') IS NOT NULL
    DROP TABLE dbo.Country;
GO

CREATE TABLE dbo.Country
(
    ID          int                NOT NULL,
    Name        nvarchar(80)       NULL,
    Code        nvarchar(6)        NULL,
    CreateDate  datetimeoffset(7)  NULL,
    TimeZone    nvarchar(1000)     NULL,
    CONSTRAINT PK_Country PRIMARY KEY CLUSTERED (ID)
);
GO
