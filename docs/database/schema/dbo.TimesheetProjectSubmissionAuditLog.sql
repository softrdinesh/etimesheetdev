/*
    Table: dbo.TimesheetProjectSubmissionAuditLog
    Recorded: 2026-10-18 - definition supplied by the database owner, replacing
              the draft proposed in this repository earlier the same day. The
              owner's version is what is recorded below; it differs from the
              draft only in that the CHECK constraint on StatusID was dropped.

    One row per status change on a project sheet - who moved it to which
    status, when, and why. The project module's answer to
    dbo.TimeSheetSubmissionAuditLog.

    One row is written at submit, and one more on every approve or reject,
    including a reject that is later resubmitted. The trail is append-only:
    nothing updates or deletes a row here, which is why there is no IsDelete
    column and no updated-by columns.

    How it differs from dbo.TimeSheetSubmissionAuditLog, and why:
      - TimesheetProjectSubmissionID is an int naming the sheet's key. The
        existing table's TimeSheetSubmissionID is a varchar(20) holding a
        timesheet CODE despite its name, which has to be explained every time
        someone reads it.
      - Remark is kept. A rejection's reason is recorded on the sheet as
        RejectionReason and again here, so the trail still says why after the
        sheet is approved and that field is cleared.

    Note which key this holds: TimesheetProjectSubmissionID, the IDENTITY of
    the sheet row - NOT SheetSubmissionCode. Should two rows ever end up with
    the same code (nothing stops it - see dbo.TimesheetProjectSubmission), the
    trail still points at exactly one of them.

    Column meanings that are not obvious from the type:
      - StatusID  the status the sheet moved TO - 1 = Submitted,
                  2 = Approved, 3 = Rejected. The same values as
                  dbo.TimesheetProjectSubmission.StatusID. Nothing in the
                  table restricts it to those three.

    Rules the API must keep, which the table does NOT:
      - StatusID being 1, 2 or 3.
      - Writing a row for every status change. Nothing obliges it, so a
        missing row looks exactly like a change that never happened.

    NO FOREIGN KEYS and NO INDEXES are declared, as on every other table in
    this folder. The primary key is the only index.
    TimesheetProjectSubmissionID is a
    dbo.TimesheetProjectSubmission.TimesheetProjectSubmissionID; CreatedBy is
    a dbo.Signup.UserID, which is not recorded at all. Rows deliberately
    OUTLIVE the sheet: nothing cascades, so the trail of what happened to a
    deleted sheet remains.
*/

IF OBJECT_ID(N'dbo.TimesheetProjectSubmissionAuditLog', N'U') IS NOT NULL
    DROP TABLE dbo.TimesheetProjectSubmissionAuditLog;
GO

CREATE TABLE dbo.TimesheetProjectSubmissionAuditLog
(
    TimesheetProjectSubmissionAuditLogID int             IDENTITY(1,1) NOT NULL,

    -- The sheet's IDENTITY, not its code.
    TimesheetProjectSubmissionID         int             NOT NULL,

    -- The status the sheet moved TO.
    StatusID                             int             NOT NULL,

    -- Why - a rejection's reason, kept after the sheet itself moves on.
    Remark                               nvarchar(500)   NULL,

    CreatedBy                            int             NOT NULL,
    CreateDate                           datetime        NOT NULL,

    CONSTRAINT PK_TimesheetProjectSubmissionAuditLog
        PRIMARY KEY CLUSTERED (TimesheetProjectSubmissionAuditLogID)
);
GO
