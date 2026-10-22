/*
    Table: dbo.TimesheetProjectSubmission
    Recorded: 2026-10-20 (CREATE TABLE supplied by the database owner)

    A user's timesheet submission for one project over one period: the hours
    and minutes logged, and its progress through submit, approve or reject.

    Mapped by:
      src/ETimeSheet.Application/Models/Entities/TimesheetProjectSubmission.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/TimesheetProjectSubmissionConfiguration.cs
      (TimesheetProjectRepository, for GET api/v1/TimesheetProject/get-timesheet-project-time-log,
      POST api/v1/TimesheetProject/add-edit-timesheet-project-time-log and
      POST api/v1/TimesheetProject/remove-timesheet-project-time-log,
      GET api/v1/TimesheetProject/get-timesheet-project-sheet,
      POST api/v1/TimesheetProject/submit-timesheet-project-sheet and
      POST api/v1/TimesheetProject/update-timesheet-project-sheet-status)

    Column quirks worth knowing:
      - One row per UserID + ProjectID + period, created by the period's first
        submit with StatusID 1 - no row exists before. A resubmit after a
        rejection updates the same row. Nothing in the database enforces it.
      - SheetSubmissionCode is the display code, in the form PSHTnnnn: the
        code the period's dbo.TimesheetProjectTimeLog rows carry, or the next
        in the sequence shared with that table for a blank sheet.
      - StatusID: 0 Pending, 1 Submitted, 2 Approved, 3 Rejected. Since
        2026-10-23 the API no longer writes 0.
      - UserID, ProjectID and OrganizationID are presumably dbo.Signup.UserID,
        dbo.TimesheetProject.ProjectID and the organization id. No foreign key
        was supplied for any of them.
      - PeriodStartDate and PeriodEndDate are plain datetime.
      - TotalHoursLogged and TotalMinutesLogged are both decimal(18,2).
      - IsDelete is an int, NOT NULL - not a bit. 1 = deleted, 0 = live.
      - dateadded is lower case and nullable. Map it by its exact name.

    STILL UNCONFIRMED:
      - Defaults, other constraints and indexes - none were supplied.
*/

IF OBJECT_ID(N'dbo.TimesheetProjectSubmission', N'U') IS NOT NULL
    DROP TABLE dbo.TimesheetProjectSubmission;
GO

CREATE TABLE dbo.TimesheetProjectSubmission
(
    TimesheetProjectSubmissionID int             IDENTITY(1,1) NOT NULL,
    SheetSubmissionCode          nvarchar(20)    NOT NULL,   -- PSHTnnnn
    StatusID                     int             NOT NULL,   -- 0 Pending, 1 Submitted, 2 Approved, 3 Rejected
    UserID                       int             NOT NULL,
    ProjectID                    int             NOT NULL,
    OrganizationID               int             NOT NULL,
    PeriodStartDate              datetime        NOT NULL,
    PeriodEndDate                datetime        NOT NULL,
    TotalHoursLogged             decimal(18, 2)  NOT NULL,
    TotalMinutesLogged           decimal(18, 2)  NOT NULL,
    SubmittedBy                  int             NULL,
    SubmittedDate                datetime        NULL,
    ApprovedBy                   int             NULL,
    ApprovedDate                 datetime        NULL,
    RejectedBy                   int             NULL,
    RejectedDate                 datetime        NULL,
    RejectionReason              nvarchar(500)   NULL,
    IsDelete                     int             NOT NULL,   -- 1 = deleted, 0 = live
    dateadded                    datetime        NULL,
    CONSTRAINT PK_TimesheetProjectSubmission
        PRIMARY KEY CLUSTERED (TimesheetProjectSubmissionID)
);
GO
