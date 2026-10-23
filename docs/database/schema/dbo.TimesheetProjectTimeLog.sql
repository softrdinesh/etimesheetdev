/*
    Table: dbo.TimesheetProjectTimeLog
    Recorded: 2026-10-20 (CREATE TABLE supplied by the database owner)
    Revised:  2026-10-22 - APPLIED by the owner: TimesheetProjectSubmissionID,
              StartTime, EndTime, DurationMinutes and Status dropped; UserID,
              ProjectID and OrganizationID added (int NOT NULL); HoursLogged
              (int NOT NULL) added in place of DurationMinutes. A time log no
              longer belongs to a submission.
    Revised:  2026-10-23 - APPLIED by the owner: IsRemoteLog int NULL added as
              the last column.

    A user's time on one project for one calendar day: the whole hours worked
    and a description. The API keeps one live row per UserID + ProjectID +
    LogDate - a later log of the same day adds its hours to that row.

    Mapped by:
      src/ETimeSheet.Application/Models/Entities/TimesheetProjectTimeLog.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/TimesheetProjectTimeLogConfiguration.cs
      (TimesheetProjectRepository, for GET api/v1/TimesheetProject/get-timesheet-project-time-log,
      POST api/v1/TimesheetProject/add-edit-timesheet-project-time-log and
      POST api/v1/TimesheetProject/remove-timesheet-project-time-log; and
      read, by UserID, ProjectID and the period's dates, for the sheet totals
      of get-timesheet-project-sheet and submit-timesheet-project-sheet)

    Column quirks worth knowing:
      - UserID is dbo.Signup.UserID, ProjectID presumably
        dbo.TimesheetProject.ProjectID, and OrganizationID the project's
        organisation. No foreign key was supplied for any of them.
      - Nothing in the database enforces one row per user, project and day:
        no unique constraint was supplied. The API enforces it.
      - SheetSubmissionCode is the PSHT code of the submission period the
        day is in (from the project's StartDate and SubmissionFrequency),
        shared by every row of one UserID + ProjectID in that period, deleted
        rows included. The period's first row takes the next code after the
        highest in this table and dbo.TimesheetProjectSubmission together. No
        dbo.TimesheetProjectSubmission row is created for it, and nothing in
        the database keeps two periods from being given the same code.
      - LogDate is a date, with no time part.
      - HoursLogged is whole hours, the day's total; it is capped per day by
        dbo.Timesheet_Users_Project_setup.Max_log_hour_day (24 when NULL).
      - Description is nvarchar(max).
      - IsRemoteLog is an int, nullable, used as a flag: the API writes 1
        (remote) or 0, or leaves it NULL when the log does not say. One value
        per day: a later log that sends it replaces it.
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
    UserID                       int             NOT NULL,
    ProjectID                    int             NOT NULL,
    OrganizationID               int             NOT NULL,
    SheetSubmissionCode          nvarchar(20)    NOT NULL,
    LogDate                      date            NOT NULL,
    HoursLogged                  int             NOT NULL,
    Description                  nvarchar(max)   NULL,
    IsDelete                     int             NOT NULL,   -- 1 = deleted, 0 = live
    CreateDate                   datetime        NOT NULL,
    CreatedBy                    int             NOT NULL,
    UpdateDate                   datetime        NULL,
    UpdatedBy                    int             NULL,
    DeletedDate                  datetime        NULL,
    DeletedBy                    int             NULL,
    IsRemoteLog                  int             NULL,
    CONSTRAINT PK_TimesheetProjectTimeLog
        PRIMARY KEY CLUSTERED (TimesheetProjectTimeLogID)
);
GO
