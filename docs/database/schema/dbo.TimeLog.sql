/*
    Table: dbo.TimeLog
    Recorded: 2026-09-14

    A record of the table as it exists in the database. This file is
    documentation and a test fixture input - it is never executed against a real
    database by the application.

    Mapped by:
      src/ETimeSheet.Application/Models/Entities/TimeLog.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/TimeLogConfiguration.cs

    Column meanings that are not obvious from the type:
      - Status    1 = Save, 2 = Draft. No other value is in use.
                  Mirrored in code by Constants.TimeLog.Status and the
                  TimeLogStatus enum.
      - IsDeleted 1 = deleted. Any other value counts as live, which is how
                  spc_GetTimeLoggedDetailsForTask tests it (IsDeleted <> 1).

    UNCONFIRMED, assumed by the mapping - correct these if the real table differs:
      - SheetID is an IDENTITY column.
      - The primary key is SheetID, clustered.
      - IsDeleted has a DEFAULT of 0.
*/

IF OBJECT_ID(N'dbo.TimeLog', N'U') IS NOT NULL
    DROP TABLE dbo.TimeLog;
GO

CREATE TABLE dbo.TimeLog
(
    SheetID       int             IDENTITY(1,1) NOT NULL,
    SheetCode     varchar(15)     NULL,
    TaskID        int             NULL,
    [Description] nvarchar(max)   NULL,
    StartTime     time(7)         NULL,
    EndTime       time(7)         NULL,
    StartDate     date            NULL,
    EndDate       date            NULL,
    CreatedBy     int             NULL,
    CreateDate    datetime        NULL,
    [Status]      int             NULL,          -- 1 = Save, 2 = Draft
    IsDeleted     int             NOT NULL CONSTRAINT DF_TimeLog_IsDeleted DEFAULT (0),
    DeleteDate    datetime        NULL,
    DeletedBy     int             NULL,
    UserID        int             NULL,
    UpdatedBy     int             NULL,
    UpdateDate    datetime        NULL,

    CONSTRAINT PK_TimeLog PRIMARY KEY CLUSTERED (SheetID)
);
GO
