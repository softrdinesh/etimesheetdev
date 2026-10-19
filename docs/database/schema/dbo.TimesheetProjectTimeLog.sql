/*
    Table: dbo.TimesheetProjectTimeLog
    Recorded: 2026-10-18 - definition supplied by the database owner, replacing
              the draft proposed in this repository earlier the same day. The
              owner's version is what is recorded below.

    One row per block of time a user logged against ONE project, for the
    project time log module. The project module's answer to dbo.TimeLog.

    What changed from the proposed draft, and what each change means:
      - TimesheetUserProjectSetupID was replaced by UserID + ProjectID. The
        row names the user and the project directly rather than the assignment
        that joins them, so a log entry CAN now name a pair that has no
        assignment in dbo.Timesheet_Users_Project_setup, and the role and
        rates that applied are not recorded with the entry. The API has to
        look the assignment up to find them.
      - TimesheetProjectSubmissionID was replaced by SheetSubmissionCode, and
        it is NOT NULL. See below - this is the biggest consequence.
      - Every CHECK constraint was dropped, and Status and IsDelete lost their
        defaults. Both are NOT NULL with no default, so EVERY insert must set
        them explicitly; there is no "live by default" any more.

    SheetSubmissionCode is NOT NULL, which means:
      An entry carries its sheet code from the moment it is created, before
      anything is submitted - the code is allocated when the user starts
      logging, not when they hand the sheet in. It is the only link between an
      entry and its dbo.TimesheetProjectSubmission row; neither table holds
      the other's key.

      HOW THE CODE IS ALLOCATED (settled 2026-10-18, implemented in
      TimesheetProjectService):
        PSH-{UserID}-{0000} - PSH-58-0001, then PSH-58-0002, and so on.

        One code covers every entry a user logs between one submission and the
        next, ACROSS EVERY PROJECT. Submitting the sheet is what ends it: the
        next entry logged after a submission finds no open sheet and starts a
        new code. No date ends a sheet, so a user who never submits keeps one
        code indefinitely, and dbo.TimesheetProject.SubmissionFrequency says
        how often a sheet is EXPECTED rather than forcing a roll-over.

        A rejected sheet is open again, so corrections are logged back onto
        the sheet they were rejected on rather than onto a new one.

      The sequence is PER USER, which is what makes allocating safe without a
      unique index: two requests racing to start the same user's first sheet
      read the same highest code and write the same next one, so they land on
      the same sheet - which is where they belonged. Two different users
      cannot collide, because their ids differ. dbo.TimeLog.SheetCode's global
      sequence can do neither, which is why it is in "Known gaps".

    Column meanings that are not obvious from the type:
      - Status    1 = Save, 2 = Draft - the same two values as
                  dbo.TimeLog.Status and Constants.TimeLog.Status. Nothing in
                  the table restricts it to those two.
      - IsDelete  1 = deleted, 0 = live. An int, not a bit, to match
                  dbo.Timesheet_Users_Project_setup and dbo.Signup. NOT NULL
                  with no default: the insert must say 0.
      - DurationMinutes
                  How long the block ran. The figure every total is built
                  from, so an entry that runs past midnight is counted once
                  and in full - unlike dbo.TimeLog, which derives its totals
                  from a StartDate/EndDate/StartTime/EndTime quartet and
                  counts an overnight entry entirely against the day it
                  starts.
      - LogDate   One day, not a range: the day the entry is logged against.
      - StartTime / EndTime
                  Optional. A user may log "3 hours on Tuesday" with no clock
                  times. Nothing makes them a pair any more, so one without
                  the other is now storable.

    A user assigned to several projects may log to all of them ON THE SAME
    DAY: that is several rows with the same UserID and LogDate and different
    ProjectID. Nothing here forbids it, by design.

    Rules the API must keep, which the table does NOT:
      - Status being 1 or 2, and IsDelete being 0 or 1.
      - DurationMinutes being more than 0 and at most 1440.
      - StartTime and EndTime being present together, and agreeing with
        DurationMinutes.
      - A deleted row carrying DeletedDate and DeletedBy, and a live row
        carrying neither.
      - UserID and ProjectID naming a real, live assignment.
      - Whether two entries of the same user may overlap in clock time, across
        projects or within one.
      - Max_log_hour_day on the assignment - a per-assignment daily cap that
        nothing reads yet.
      - Not editing an entry whose sheet has already been submitted.

    NO FOREIGN KEYS and NO INDEXES are declared, as on every other table in
    this folder. The primary key is the only index. UserID, CreatedBy,
    UpdatedBy and DeletedBy are dbo.Signup.UserID, which is not recorded at
    all; ProjectID is a dbo.TimesheetProject.ProjectID; SheetSubmissionCode is
    a dbo.TimesheetProjectSubmission.SheetSubmissionCode.

    STILL UNCONFIRMED:
      - Whether an entry may be logged against a day outside the project's
        StartDate / EndDate, or on a holiday or weekend day for that project
        (dbo.TimesheetHolidaysetup, dbo.TimeSheet_Weekend_setup).
      - Whether Description should be required. It is the only record of what
        was worked on, since there is no task.
*/

IF OBJECT_ID(N'dbo.TimesheetProjectTimeLog', N'U') IS NOT NULL
    DROP TABLE dbo.TimesheetProjectTimeLog;
GO

CREATE TABLE dbo.TimesheetProjectTimeLog
(
    TimesheetProjectTimeLogID    int             IDENTITY(1,1) NOT NULL,
    UserID                       int             NOT NULL,
    ProjectID                    int             NOT NULL,

    -- Carried from the moment the entry is created, not set at submit.
    SheetSubmissionCode          nvarchar(20)    NOT NULL,

    -- The day the work was done. One day, not a range.
    LogDate                      date            NOT NULL,

    -- Optional clock times.
    StartTime                    time(7)         NULL,
    EndTime                      time(7)         NULL,

    -- The figure every total is built from.
    DurationMinutes              int             NOT NULL,

    [Description]                nvarchar(max)   NULL,
    [Status]                     int             NOT NULL,       -- 1 = Save, 2 = Draft
    IsDelete                     int             NOT NULL,       -- 1 = deleted, 0 = live
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
