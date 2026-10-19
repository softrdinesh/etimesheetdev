/*
    Table: dbo.TimesheetProjectSubmission
    Recorded: 2026-10-18 - definition supplied by the database owner, replacing
              the draft proposed in this repository earlier the same day. The
              owner's version is what is recorded below.

    One row per submitted sheet, for the project time log module. The project
    module's answer to dbo.TimesheetSubmission.

    What a sheet is, and what joins it to its entries:
      SheetSubmissionCode. The entries that belong to this sheet are the rows
      of dbo.TimesheetProjectTimeLog carrying the same code; neither table
      holds the other's key. The code is on the entry from the moment it is
      created (it is NOT NULL there), so a sheet is a code that already exists
      before anything is handed in.

      One sheet therefore covers everything the user logged under that code -
      every project included, which is the per-user, all-projects grain that
      was asked for. The per-project breakdown is got by grouping the entries
      by ProjectID, which is why the detail table proposed earlier was dropped
      rather than recorded.

    What changed from the proposed draft, and what each change means:
      - UserID and OrganizationID were dropped. Whose sheet it is now comes
        from SubmittedBy, or from the entries that share the code. An
        organisation check is not possible from this row alone, the same
        position dbo.TimesheetSubmission is in.
      - PeriodStart and PeriodEnd were dropped. A sheet does not say what
        period it covers; the period is whatever its entries' LogDates span.
      - SubmissionCode became SheetSubmissionCode, nvarchar(20), and NOT NULL.
      - Every CHECK constraint was dropped, and the totals and IsDelete lost
        their defaults - all three are NOT NULL with no default, so every
        insert must set them, including a 0.

    AN EMPTY SHEET NEEDS A DECISION. It was asked for - a user who logged
    nothing may still submit the period - and this row still supports one:
    SheetSubmissionCode, SubmittedBy and totals of 0, with no entries. But
    with no UserID, no period columns and no entries to derive either from,
    such a row cannot say WHICH PERIOD it covers. Either SheetSubmissionCode
    has to encode the period, or an empty sheet has to be stored some other
    way. Nothing recorded settles it.

    Column meanings that are not obvious from the type:
      - StatusID  1 = Submitted, 2 = Approved, 3 = Rejected - the same values
                  as dbo.TimesheetSubmission.StatusID and
                  Constants.TimesheetSubmission.Status. Nothing in the table
                  restricts it to those three.
      - IsDelete  1 = deleted, 0 = live. NOT NULL with no default.
      - RejectionReason
                  Why a sheet was rejected. The existing module records that a
                  sheet was rejected and by whom, but never why. Cleared if
                  the sheet is later approved; the reason survives in
                  dbo.TimesheetProjectSubmissionAuditLog.Remark.

    Resubmitting after a rejection:
      Nothing about the code or the period is part of the primary key, so a
      rejected sheet can be left where it is and another submitted. That is
      the one thing dbo.TimesheetSubmission cannot do - its primary key on
      Timesheetcode means a rejected sheet there can never be corrected and
      handed in again. What the module does with the REJECTED sheet's entries,
      which still carry its code, is its own decision.

    Rules the API must keep, which the table does NOT:
      - StatusID being 1, 2 or 3, and IsDelete being 0 or 1.
      - SheetSubmissionCode being unique among live sheets. There is no unique
        index, so two simultaneous submits of the same code can BOTH succeed
        and leave two rows for one sheet - and a later read cannot tell which
        is authoritative. The existing module is protected from this by its
        primary key; this one is not.
      - The totals matching the entries carrying the code. Nothing here
        recomputes them.
      - An approved sheet carrying ApprovedBy and ApprovedDate, a rejected one
        carrying RejectedBy and RejectedDate, and a sheet that is only
        submitted carrying neither pair. Note that these columns describe the
        sheet's CURRENT status: re-reviewing an approved sheet as rejected
        means clearing the approval pair as the rejection is written. The
        history is in the audit log, which is append-only.
      - Who may approve or reject - the table takes any UserID.

    NO FOREIGN KEYS and NO INDEXES are declared, as on every other table in
    this folder. The primary key is the only index. SubmittedBy, ApprovedBy
    and RejectedBy are dbo.Signup.UserID, which is not recorded at all.
*/

IF OBJECT_ID(N'dbo.TimesheetProjectSubmission', N'U') IS NOT NULL
    DROP TABLE dbo.TimesheetProjectSubmission;
GO

CREATE TABLE dbo.TimesheetProjectSubmission
(
    TimesheetProjectSubmissionID int             IDENTITY(1,1) NOT NULL,

    -- The sheet. Its entries are the dbo.TimesheetProjectTimeLog rows
    -- carrying the same code.
    SheetSubmissionCode          nvarchar(20)    NOT NULL,

    TotalHours                   decimal(18, 2)  NOT NULL,
    TotalMinutes                 decimal(18, 2)  NOT NULL,

    StatusID                     int             NOT NULL,   -- 1 Submitted, 2 Approved, 3 Rejected

    SubmittedBy                  int             NOT NULL,
    SubmittedDate                datetime        NOT NULL,

    ApprovedBy                   int             NULL,
    ApprovedDate                 datetime        NULL,

    RejectedBy                   int             NULL,
    RejectedDate                 datetime        NULL,
    RejectionReason              nvarchar(500)   NULL,

    IsDelete                     int             NOT NULL,   -- 1 = deleted, 0 = live

    CONSTRAINT PK_TimesheetProjectSubmission
        PRIMARY KEY CLUSTERED (TimesheetProjectSubmissionID)
);
GO
