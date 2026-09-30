/*
    Table: dbo.TimesheetSubmission
    Recorded: 2026-09-30 (column list supplied by the database owner)
    Revised:  2026-09-30 - owner confirmed Timesheetcode is the primary key.

    One row per submitted timesheet, keyed by Timesheetcode, carrying the
    submitted totals and the approve / reject trail.

    Mapped by:
      src/ETimeSheet.Application/Models/Entities/TimesheetSubmission.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/TimesheetSubmissionConfiguration.cs

    Written by:
      src/ETimeSheet.Infrastructure/Repositories/SheetSubmissionRepository.cs
      (insert, via POST /api/v1/SheetSubmission/submit-timesheet)

    Column meanings that are not obvious from the type:
      - StatusID  1 = Submitted, 2 = Approved, 3 = Rejected (stated by the
                  owner 2026-09-30). Mirrored in code by
                  Constants.TimesheetSubmission.Status.

    Column quirks worth knowing:
      - TotalHours and TotalMins are both decimal(18,2), NOT int.
      - SubmittedBy and SubmittedDate are NOT NULL; the approval and rejection
        columns, and StatusID, are all NULLABLE.
      - ApprovedDate, RejectedDate and SubmittedDate are plain datetime, like
        dbo.TimeLog and dbo.TimesheetMasterSetup.
      - Timesheetcode is varchar (not nvarchar), collation
        SQL_Latin1_General_CP1_CI_AS, and is the primary key - a sheet can be
        submitted only once, which the database itself enforces.

    STILL UNCONFIRMED:
      - The primary key's constraint name, and whether it is clustered (SQL
        Server's default for a primary key, so recorded as such). The name
        PK_TimesheetSubmission below is a placeholder.
      - Whether any column carries a default, and whether SubmittedBy /
        ApprovedBy / RejectedBy / StatusID have foreign keys.
*/

IF OBJECT_ID(N'dbo.TimesheetSubmission', N'U') IS NOT NULL
    DROP TABLE dbo.TimesheetSubmission;
GO

CREATE TABLE dbo.TimesheetSubmission
(
    Timesheetcode  varchar(15)     COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
    TotalHours     decimal(18, 2)  NOT NULL,
    TotalMins      decimal(18, 2)  NOT NULL,
    SubmittedBy    int             NOT NULL,
    ApprovedBy     int             NULL,
    ApprovedDate   datetime        NULL,
    RejectedBy     int             NULL,
    RejectedDate   datetime        NULL,
    StatusID       int             NULL,
    SubmittedDate  datetime        NOT NULL,

    CONSTRAINT PK_TimesheetSubmission PRIMARY KEY CLUSTERED (Timesheetcode)
);
GO
