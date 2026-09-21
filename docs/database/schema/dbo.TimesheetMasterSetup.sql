/*
    Table: dbo.TimesheetMasterSetup
    Recorded: 2026-09-15 (column list confirmed against the live database 2026-09-15)
    Revised:  2026-09-17 - StartDay, EndDay and Exceptionday changed from
              char(2)/char(2)/char(3) to int, per the column list supplied by the
              database owner. They now hold dbo.DayMaster.DayID values.
    Revised:  2026-09-20 - TimeZone nvarchar(100) NULL added by the database
              owner. Holds ONE IANA zone id for this setup.

    Per-user timesheet limits and working-week settings.

    Mapped by:
      src/ETimeSheet.Application/Models/Entities/TimesheetMasterSetup.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/TimesheetMasterSetupConfiguration.cs

    Column quirks worth knowing:
      - MaxTimeinhrs / MaxTiminmins are time(7), NOT numbers. "8 hours" is
        stored as 08:00:00.
      - StartDay / EndDay / Exceptionday are int and reference
        dbo.DayMaster.DayID: 1 = Monday ... 7 = Sunday, the ISO-8601 numbering.
        They are NOT System.DayOfWeek values, which number Sunday 0.
        No FOREIGN KEY constraint is declared to dbo.DayMaster - the values are
        a reference by convention, so a row can legally hold an id the lookup
        does not contain. The API treats an unknown id as "no week configured"
        rather than failing the request.
      - TimeZone holds exactly ONE IANA zone id - "Europe/London". It is NOT
        the comma-separated list that dbo.Country.TimeZone holds; it is one
        entry chosen from that list. The API never takes it on trust: the save
        reads the country's list, uses it outright when the country has a single
        zone, and otherwise requires the payload to name one of them.
      - Soft delete is IsDelete, a NULLABLE bit. dbo.TimeLog spells the same idea
        IsDeleted and types it int NOT NULL. The two tables genuinely differ, so
        this entity does not share TimeLog's AuditableEntity base.
      - "Deletedby" is spelled with a lower-case b.

    CONFIRMED 2026-09-15, by querying the table through the API:
      - The table had exactly the 18 columns confirmed that day; TimeZone,
        added by the database owner on 2026-09-20, makes 19. There is NO
        CanUserLoggedPreDayTime column: selecting it returns
        "Invalid column name 'CanUserLoggedPreDayTime'".
        spc_GetTimesheetMasterSetupByUserID nonetheless RETURNS a column of that
        name, so the procedure derives it rather than reading it. It therefore
        belongs only on that procedure's keyless result type
        (TimesheetMasterSetupDetail, in Models/TimeLog.cs) - never on the entity.
      - Every column except SetupID is nullable, and real rows do leave most of
        them null.

    STILL UNCONFIRMED:
      - SetupID is assumed to be an IDENTITY column. Inserts through the API do
        get a generated id back, which is consistent with that.
      - The primary key is assumed to be SetupID, clustered.
      - Whether the rows that held char day codes before the 2026-09-17 change
        were converted to the matching DayMaster ids, or left as they were. The
        API reads whatever is there and ignores an id outside 1-7.

    Written by:
      src/ETimeSheet.Infrastructure/Repositories/AdminRepository.cs
      (add / edit / soft delete, via the Admin module)
*/

IF OBJECT_ID(N'dbo.TimesheetMasterSetup', N'U') IS NOT NULL
    DROP TABLE dbo.TimesheetMasterSetup;
GO

CREATE TABLE dbo.TimesheetMasterSetup
(
    SetupID          int        IDENTITY(1,1) NOT NULL,
    MaxTimeinhrs     time(7)    NULL,
    MaxTiminmins     time(7)    NULL,
    UserID           int        NULL,
    OrganizationID   int        NULL,
    ContractType     int        NULL,
    CreateDate       datetime   NULL,
    CreatedBy        int        NULL,
    StartDay         int        NULL,
    EndDay           int        NULL,
    CountryID        int        NULL,
    IsDelete         bit        NULL,
    UpdateDate       datetime   NULL,
    UpdatedBy        int        NULL,
    DeleteDate       datetime   NULL,
    Deletedby        int        NULL,
    Exceptionday     int        NULL,
    TimeEntryLockAt  time(7)    NULL,
    TimeZone         nvarchar(100) NULL,
    CONSTRAINT PK_TimesheetMasterSetup PRIMARY KEY CLUSTERED (SetupID)
);
GO
