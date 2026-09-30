/*
    Procedure: dbo.spc_GetSubmittedSheetList
    Recorded: 2026-09-30 - body below is the deployed version, as supplied by
              the database owner. Handed over as ALTER PROCEDURE; recorded as
              CREATE OR ALTER so it runs against the empty database the
              integration fixture builds.
              Differs from the draft: FirstName / LastName (NULL placeholders)
              replaced by one column, Name, read from dbo.Signup.name.

    Returns the submitted timesheets for one organisation, one row per
    dbo.TimesheetSubmission row - every user's, or one user's.

    Called by:
      src/ETimeSheet.Infrastructure/Repositories/SheetSubmissionRepository.cs
      (POST /api/v1/SheetSubmission/get-submitted-sheet-list)
    Result set mapped by:
      src/ETimeSheet.Application/Models/SheetSubmission.cs (SubmittedSheetDetail)
      src/ETimeSheet.Infrastructure/Data/Context.cs (ConfigureStoredProcedureResults)

    Parameters:
      @PAdminID  the admin asking. NOT USED YET - see PLACEHOLDER 1.
      @PUserID   0 = every user in the organisation; any other value = that
                 user's submissions only.
      @POrgID    the organisation. Filters on dbo.Signup.OrganizationID, the
                 same tenant boundary spc_GetEmployeeListByPOrgID uses.

    PLACEHOLDERS still open in the deployed body:
      1. Admin check    @PAdminID is not checked against anything.
      4. StatusText     'Submitted' for StatusID 1, NULL for any other value -
                        only 1 is defined so far.
    (2 and 3, FirstName / LastName, were resolved as the single Name column.)

    The week, per sheet:
      WeekStartDate is the setup's StartDay (dbo.DayMaster.DayID, 1 = Monday
      ... 7 = Sunday) on or before the sheet's EARLIEST live entry - the same
      anchor the time log save uses to share one SheetCode across a week, and
      the same Monday fallback when there is no usable StartDay.
      WeekEndDate is the setup's EndDay after it (StartDay..EndDay, wrapping
      past Sunday), or six days after the start when either day is missing.
      Both are NULL when the sheet has no live entry left to date it by.

    Hours, per sheet:
      TotalHoursWorked   dbo.TimesheetSubmission.TotalHours - the figure
                         SUBMITTED, not a recount of today's entries.
      TotalHoursExpected working days x the setup's daily time
                         (MaxTimeinhrs hours + minutes, plus the minutes of
                         MaxTiminmins), as spc_GetUserDashboardSummaryByUserID
                         computes it. NULL when there is no setup or it is
                         incomplete.
      TotalHoursDrift    worked - expected, NULL whenever expected is.
      Expected and drift use the user's CURRENT setup: a setup changed after
      a submission changes them for older sheets too.

    The SELECT list is a contract: the keyless result type binds by column
    name, so adding, removing or renaming a column here requires the matching
    change to SubmittedSheetDetail and Context.ConfigureStoredProcedureResults.

    The tables are recorded at:
      ../schema/dbo.TimesheetSubmission.sql
      ../schema/dbo.TimesheetMasterSetup.sql
      ../schema/dbo.TimeLog.sql
      ../schema/dbo.DayMaster.sql
      dbo.Signup - NOT currently recorded.
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_GetSubmittedSheetList]
        @PAdminID INT,
        @PUserID  INT,
        @POrgID   INT
AS
BEGIN
    SET NOCOUNT ON;

    -- =========================================================
    -- PLACEHOLDER 1: admin check
    --   e.g. return nothing unless @PAdminID is an admin of @POrgID.
    -- =========================================================

    SELECT
        -- Who
        ts.SubmittedBy                          AS UserID,
        s.name                                  AS Name,   -- PLACEHOLDER 2
        s.Email,    

        -- The sheet and its week
        ts.Timesheetcode                        AS SheetCode,
        wk.WeekStartDate,
        DATEADD(DAY, ISNULL(wd.WorkingDays, 7) - 1, wk.WeekStartDate) AS WeekEndDate,

        tms.StartDay,
        sd.[Day]                                AS StartDayName,
        tms.EndDay,
        ed.[Day]                                AS EndDayName,

        -- Hours
        ts.TotalHours                           AS TotalHoursWorked,
        CAST(ROUND(ex.ExpectedMinutes / 60.0, 2) AS DECIMAL(18, 2)) AS TotalHoursExpected,
        CAST(ROUND((ts.TotalMins - ex.ExpectedMinutes) / 60.0, 2) AS DECIMAL(18, 2)) AS TotalHoursDrift,

        -- Status
        ts.StatusID                             AS [Status],
        CASE ts.StatusID                                        -- PLACEHOLDER 4
            WHEN 1 THEN 'Submitted'
        END                                     AS StatusText,

        ts.SubmittedDate

    FROM TimesheetSubmission ts
    INNER JOIN Signup s ON s.UserID = ts.SubmittedBy

    -- The user's live setup. Lowest SetupID wins should there ever be two,
    -- as in spc_GetUserDashboardSummaryByUserID.
    OUTER APPLY
    (
        SELECT TOP (1)
            x.StartDay,
            x.EndDay,
            x.MaxTimeinhrs,
            x.MaxTiminmins
        FROM TimesheetMasterSetup x
        WHERE x.UserID = ts.SubmittedBy
          AND ISNULL(x.IsDelete, 0) = 0
        ORDER BY x.SetupID
    ) tms

    LEFT JOIN DayMaster sd ON sd.DayID = tms.StartDay
    LEFT JOIN DayMaster ed ON ed.DayID = tms.EndDay

    -- The sheet's earliest live entry, which dates the week.
    OUTER APPLY
    (
        SELECT MIN(tl.StartDate) AS FirstDate
        FROM TimeLog tl
        WHERE tl.SheetCode = ts.Timesheetcode
          AND tl.UserID    = ts.SubmittedBy
          AND tl.IsDeleted <> 1
    ) fe

    -- Start of that entry's week. '19000101' is a Monday, so the DayID
    -- arithmetic is right whatever DATEFIRST is set to.
    OUTER APPLY
    (
        SELECT DATEADD(DAY,
                   -(((DATEDIFF(DAY, '19000101', fe.FirstDate) % 7) + 1
                      - CASE WHEN tms.StartDay BETWEEN 1 AND 7 THEN tms.StartDay ELSE 1 END
                      + 7) % 7),
                   fe.FirstDate) AS WeekStartDate
    ) wk

    -- Working days StartDay..EndDay inclusive, wrapping past Sunday.
    OUTER APPLY
    (
        SELECT CASE
                   WHEN tms.StartDay BETWEEN 1 AND 7 AND tms.EndDay BETWEEN 1 AND 7
                       THEN ((tms.EndDay - tms.StartDay + 7) % 7) + 1
               END AS WorkingDays
    ) wd

    -- NULL when either side is NULL - no setup, or an incomplete one.
    OUTER APPLY
    (
        SELECT wd.WorkingDays
             * (  DATEPART(HOUR,   tms.MaxTimeinhrs) * 60
                + DATEPART(MINUTE, tms.MaxTimeinhrs)
                + ISNULL(DATEPART(MINUTE, tms.MaxTiminmins), 0)) AS ExpectedMinutes
    ) ex

    WHERE s.OrganizationID = @POrgID
      AND s.isdelete = 0
      AND (@PUserID = 0 OR ts.SubmittedBy = @PUserID)

    ORDER BY ts.SubmittedDate DESC;
END
GO
