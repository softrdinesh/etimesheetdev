/*
    Table: dbo.TimesheetProjectTimeLog
    Recorded: 2026-10-20 (CREATE TABLE supplied by the database owner)

    One time entry within a project timesheet submission: the day, the start
    and end time, the minutes worked and a description. Many rows per
    dbo.TimesheetProjectSubmission.

    Mapped by:
      src/ETimeSheet.Application/Models/Entities/TimesheetProjectTimeLog.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/TimesheetProjectTimeLogConfiguration.cs
      (TimesheetProjectRepository, for GET api/v1/TimesheetProject/get-timesheet-project-time-log,
      POST api/v1/TimesheetProject/add-edit-timesheet-project-time-log and
      POST api/v1/TimesheetProject/remove-timesheet-project-time-log)

    Column quirks worth knowing:
      - TimesheetProjectSubmissionID is presumably
        dbo.TimesheetProjectSubmission.TimesheetProjectSubmissionID, and
        SheetSubmissionCode repeats that submission's PSHTnnnn code. No foreign
        key was supplied, so nothing keeps the two in step.
      - LogDate is a date, with no time part.
      - StartTime and EndTime are time(7) and nullable; DurationMinutes is a
        NOT NULL int, so an entry can carry a duration without a start or end.
      - Description is nvarchar(max).
      - Status: 1 = Save, 2 = Draft.
      - IsDelete is an int, NOT NULL - not a bit. 1 = deleted, 0 = live.
      - CreateDate, UpdateDate and DeletedDate are plain datetime.

    STILL UNCONFIRMED:
      - Defaults, other constraints and indexes - none were supplied.
*/

IF OBJECT_ID(N'dbo.TimesheetProjectTimeLog', N'U') IS NOT NULL
    DROP TABLE dbo.TimesheetProjectTimeLog;
GO

CREATE TABLE dbo.TimesheetProjectTimeLog
(
    TimesheetProjectTimeLogID    int             IDENTITY(1,1) NOT NULL,
    TimesheetProjectSubmissionID int             NOT NULL,
    SheetSubmissionCode          nvarchar(20)    NOT NULL,
    LogDate                      date            NOT NULL,
    StartTime                    time(7)         NULL,
    EndTime                      time(7)         NULL,
    DurationMinutes              int             NOT NULL,
    Description                  nvarchar(max)   NULL,
    Status                       int             NOT NULL,   -- 1 = Save, 2 = Draft
    IsDelete                     int             NOT NULL,   -- 1 = deleted, 0 = live
    CreateDate                   datetime        NOT NULL,
    CreatedBy                    int             NOT NULL,
    UpdateDate                   datetime        NULL,
    UpdatedBy                    int             NULL,
    DeletedDate                  datetime        NULL,
    DeletedBy                    int             NULL,
    CONSTRAINT PK_TimesheetProjectTimeLog
        PRIMARY KEY CLUSTERED (TimesheetProjectTimeLogID)
);
GO
