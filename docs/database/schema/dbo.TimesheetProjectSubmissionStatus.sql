/*
    Table: dbo.TimesheetProjectSubmissionStatus
    Recorded: 2026-10-23 (written at the database owner's request, to be
              created by hand; confirm here once it has been applied)

    Lookup table for the statuses of a project timesheet:
    dbo.TimesheetProjectSubmission.StatusID and
    dbo.TimesheetProjectSubmissionAuditLog.StatusID. Reference data: the rows
    below are the whole table and are inserted with the schema.

    READ-ONLY TO THE API. Nothing maps or reads it yet - the API still names
    statuses from Constants.TimesheetProjectSubmission.Status.

    Mapped by:
      nothing yet.

    Column quirks worth knowing:
      - StatusID is NOT an IDENTITY: the ids are written explicitly, because
        they must equal the values already stored in
        dbo.TimesheetProjectSubmission.StatusID, and
        Constants.TimesheetProjectSubmission.Status in code:
          0 = Pending    (rows written before 2026-10-23; the API no longer
                          writes it)
          1 = Submitted
          2 = Approved
          3 = Rejected
      - Status is nvarchar(20) and UNIQUE, so it is safe to look a row up by
        name.
      - No foreign key from dbo.TimesheetProjectSubmission.StatusID to this
        table is declared.
*/

IF OBJECT_ID(N'dbo.TimesheetProjectSubmissionStatus', N'U') IS NOT NULL
    DROP TABLE dbo.TimesheetProjectSubmissionStatus;
GO

CREATE TABLE dbo.TimesheetProjectSubmissionStatus
(
    StatusID  int           NOT NULL,
    Status    nvarchar(20)  NOT NULL,

    CONSTRAINT PK_TimesheetProjectSubmissionStatus
        PRIMARY KEY CLUSTERED (StatusID),
    CONSTRAINT UQ_TimesheetProjectSubmissionStatus_Status
        UNIQUE (Status)
);
GO

INSERT INTO dbo.TimesheetProjectSubmissionStatus (StatusID, Status)
VALUES
    (0, N'Pending'),
    (1, N'Submitted'),
    (2, N'Approved'),
    (3, N'Rejected');
GO
