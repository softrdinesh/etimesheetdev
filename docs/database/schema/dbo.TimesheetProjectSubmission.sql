/*
    Table: dbo.TimesheetProjectSubmission
    Recorded: 2026-10-20 (CREATE TABLE supplied by the database owner)
    Revised:  2026-11-10 - DRAFT, requested by the owner, NOT YET APPLIED:
              IsAutoApprove int NULL added - 1 when the
              AutoApproveProjectSheet job approved the sheet, 0 when a person
              did, NULL on a row that predates the column. Applied with:
                ALTER TABLE dbo.TimesheetProjectSubmission ADD IsAutoApprove int NULL;
              Nullable and without a default on purpose: that is what lets it
              be added to a table that already has rows, and it leaves those
              rows NULL rather than claiming a person approved them.
              Change to applied once the owner has run it.

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

    Also written by:
      src/ETimeSheet.Infrastructure/Repositories/SchedulerRepository.cs
      (the AutoApproveProjectSheet job: a sheet left StatusID 1 longer than
      its project's dbo.TimesheetProject.AutoApproveInDays is moved to
      StatusID 2, with ApprovedBy set to the configured
      AutoApprove:ApprovedByUserId and ApprovedDate to the run's UTC time.
      No request is involved, and no approver pressed anything.)

    Column quirks worth knowing:
      - One row per UserID + ProjectID + period, created by the period's first
        submit with StatusID 1 - no row exists before. A resubmit after a
        rejection updates the same row. Nothing in the database enforces it.
      - SheetSubmissionCode is the display code, in the form PSHTnnnn: the
        code the period's dbo.TimesheetProjectTimeLog rows carry, or the next
        in the sequence shared with that table for a blank sheet.
      - StatusID: 0 Pending, 1 Submitted, 2 Approved, 3 Rejected. Since
        2026-10-23 the API no longer writes 0.
      - IsAutoApprove says WHO did the approving, not whether the sheet is
        approved: 1 only on a sheet the AutoApproveProjectSheet job approved,
        0 on one a person approved through
        update-timesheet-project-sheet-status. An int, NULLABLE, with no
        default - the same shape as IsDelete on
        dbo.Timesheet_Users_Project_setup.
        Three values, and the difference matters:
          1     the scheduler approved it.
          0     the API wrote 0 - a person approved it, or the sheet was
                submitted or resubmitted and is awaiting a decision.
          NULL  nothing has ever written the column: a row that predates it.
        So 0 does NOT mean "a person approved it" on its own. Read it
        together with StatusID, never alone.
        The API does not pass NULL on: TimesheetProjectMappings reads a NULL
        as 0, so the isAutoApprove of a response is always 0 or 1 and a
        client never sees the third state. The column keeps it, because the
        row genuinely does not know. Backfilling the NULLs to 0 is the
        owner's call and nothing in the API needs it.
      - ApprovedBy is not always the project's approver: the
        AutoApproveProjectSheet job writes the configured
        AutoApprove:ApprovedByUserId there. The audit row in
        dbo.TimesheetProjectSubmissionAuditLog carries the same id, so an
        automatic approval is told apart from a manual one by that id, not by
        a column of its own.
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
    -- 1 = approved by the scheduler, 0 = by a person, NULL = never written
    IsAutoApprove                int             NULL,
    CONSTRAINT PK_TimesheetProjectSubmission
        PRIMARY KEY CLUSTERED (TimesheetProjectSubmissionID)
);
GO
