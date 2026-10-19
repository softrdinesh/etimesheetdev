/*
    Table: dbo.TimesheetProjectSubmissionAuditLog
    Recorded: 2026-10-20 (CREATE TABLE supplied by the database owner)

    One row per status change on a project timesheet submission - who moved it
    to which status, and when. The project-submission counterpart of
    dbo.TimeSheetSubmissionAuditLog.

    Mapped by:
      src/ETimeSheet.Application/Models/Entities/TimesheetProjectSubmissionAuditLog.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/TimesheetProjectSubmissionAuditLogConfiguration.cs

    Written by:
      TimesheetProjectRepository through EF Core - one row per status change,
      in the same save as the change: StatusID 1 from
      POST api/v1/TimesheetProject/submit-timesheet-project-sheet, and 2 or 3
      from POST api/v1/TimesheetProject/update-timesheet-project-sheet-status.

    Column meanings that are not obvious from the type:
      - StatusID  the status the submission moved to - presumably the same
                  values as dbo.TimesheetProjectSubmission.StatusID:
                  0 = Pending, 1 = Submitted, 2 = Approved, 3 = Rejected.

    Column quirks worth knowing:
      - Every column is NOT NULL.
      - TimesheetProjectSubmissionAuditLogID is IDENTITY: a sequence number and
        nothing more. The database generates it.
      - TimesheetProjectSubmissionID is presumably
        dbo.TimesheetProjectSubmission.TimesheetProjectSubmissionID. No foreign
        key was supplied.
      - CreateDate is plain datetime.

    STILL UNCONFIRMED:
      - Defaults, other constraints and indexes - none were supplied.
*/

IF OBJECT_ID(N'dbo.TimesheetProjectSubmissionAuditLog', N'U') IS NOT NULL
    DROP TABLE dbo.TimesheetProjectSubmissionAuditLog;
GO

CREATE TABLE dbo.TimesheetProjectSubmissionAuditLog
(
    TimesheetProjectSubmissionAuditLogID int             IDENTITY(1,1) NOT NULL,
    TimesheetProjectSubmissionID         int             NOT NULL,
    StatusID                             int             NOT NULL,
    CreatedBy                            int             NOT NULL,
    CreateDate                           datetime        NOT NULL,
    CONSTRAINT PK_TimesheetProjectSubmissionAuditLog
        PRIMARY KEY CLUSTERED (TimesheetProjectSubmissionAuditLogID)
);
GO
