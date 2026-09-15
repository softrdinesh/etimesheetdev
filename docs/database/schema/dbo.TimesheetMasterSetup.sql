/*
    Table: dbo.TimesheetMasterSetup
    Recorded: 2026-09-15 (column list confirmed against the live database 2026-09-15)

    Per-user timesheet limits and working-week settings.

    Mapped by:
      src/ETimeSheet.Application/Models/Entities/TimesheetMasterSetup.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/TimesheetMasterSetupConfiguration.cs

    Column quirks worth knowing:
      - MaxTimeinhrs / MaxTiminmins are time(7), NOT numbers. "8 hours" is
        stored as 08:00:00.
      - StartDay / EndDay are char(2) and Exceptionday is char(3) - fixed width,
        so values come back blank-padded.
      - Soft delete is IsDelete, a NULLABLE bit. dbo.TimeLog spells the same idea
        IsDeleted and types it int NOT NULL. The two tables genuinely differ, so
        this entity does not share TimeLog's AuditableEntity base.
      - "Deletedby" is spelled with a lower-case b.

    CONFIRMED 2026-09-15, by querying the table through the API:
      - The table has exactly the 18 columns below. There is NO
        CanUserLoggedPreDayTime column: selecting it returns
        "Invalid column name 'CanUserLoggedPreDayTime'".
        spc_GetTimesheetMasterSetupByUserID nonetheless RETURNS a column of that
        name, so the procedure derives it rather than reading it. It therefore
        belongs only on that procedure's keyless result type
        (Models/Results/TimesheetMasterSetupDetail.cs) - never on the entity.
      - Every column except SetupID is nullable, and real rows do leave most of
        them null.

    STILL UNCONFIRMED:
      - SetupID is assumed to be an IDENTITY column. Inserts through the API do
        get a generated id back, which is consistent with that.
      - The primary key is assumed to be SetupID, clustered.

    Written by:
      src/ETimeSheet.Infrastructure/Repositories/AdminSetupRepository.cs
      (add / edit / soft delete, via the AdminSetup module)
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
    StartDay         char(2)    NULL,
    EndDay           char(2)    NULL,
    CountryID        int        NULL,
    IsDelete         bit        NULL,
    UpdateDate       datetime   NULL,
    UpdatedBy        int        NULL,
    DeleteDate       datetime   NULL,
    Deletedby        int        NULL,
    Exceptionday     char(3)    NULL,
    TimeEntryLockAt  time(7)    NULL,
    CONSTRAINT PK_TimesheetMasterSetup PRIMARY KEY CLUSTERED (SetupID)
);
GO
