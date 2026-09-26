/*
    Procedure: dbo.spc_GetAdminDashboardSummaryByOrgID
    Status:    DRAFT for review - not applied to any database.

    Returns one row of dashboard figures for one organisation:
      - total employees
      - full-time and part-time employees
      - hours logged this week, across all employees
      - hours expected this week, across all employees
      - pending and approved timesheets (STATIC placeholders - see below)

    Parameters:
      @POrgID  the organisation (dbo.Signup.OrganizationID).

    Employees:
      Every non-deleted dbo.Signup row in the organisation (isdelete = 0).
      There is no role filter, matching spc_GetEmployeeListByPOrgID, whose
      RoleID = 2 filter is commented out - so administrators and managers
      are counted too.

    Full time / part time:
      Employees in the organisation who have a live setup row
      (TimesheetMasterSetup.IsDelete not 1) with ContractType 1 (Full Time)
      or 2 (Part Time). An employee with no setup, or with any other
      ContractType, is in TotalEmployees but in neither of these, so the
      two need not add up to the total. When a user has more than one live
      setup row, the lowest SetupID is used, so nobody is counted twice.

    "This week":
      Each employee's OWN timesheet week - anchored on their setup's
      StartDay and seven days long, exactly as in
      spc_GetUserDashboardSummaryByUserID - so the organisation totals are
      the sum of what each employee's own dashboard shows. An employee with
      no setup (or no usable StartDay) falls back to a Monday week.
      "Today" is the DATABASE SERVER's date - CAST(GETDATE() AS DATE) - as in
      the user dashboard procedure.

    Expected time:
      Per employee, working days in StartDay..EndDay (wrapping past Sunday)
      x daily time (MaxTimeinhrs hours + minutes, plus the minutes of
      MaxTiminmins), summed across the organisation. An employee with no
      setup, or an incomplete one, contributes 0. Exceptionday adds nothing.

    Logged time:
      Every live entry (IsDeleted <> 1), Save and Draft alike, by every
      employee in the organisation - with or without a setup. Incomplete
      rows and rows that do not run forwards are skipped; an entry crossing
      the week boundary counts only the part inside that employee's week.

    Pending / approved timesheets:
      STATIC. Hard-coded literals with no logic behind them, as requested,
      until an approval workflow exists. They return 0 for every
      organisation.

    Result:
      Always exactly one row - an organisation with no employees gets zeros,
      not an empty result. Time totals come back as whole minutes and as
      text ("327h 30m"), like the user dashboard.

    The tables are recorded at:
      ../schema/dbo.TimesheetMasterSetup.sql
      ../schema/dbo.TimeLog.sql
      ../schema/dbo.Signup.sql  (PARTIAL - listed in ../README.md)
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_GetAdminDashboardSummaryByOrgID]
    @POrgID INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Today DATE = CAST(GETDATE() AS DATE);

    -- DayMaster numbering: 1 = Monday ... 7 = Sunday. '19000101' is a
    -- Monday, so the % 7 is 0 on a Monday whatever DATEFIRST is set to.
    DECLARE @TodayDayID INT = (DATEDIFF(DAY, '19000101', @Today) % 7) + 1;

    ;WITH Employees AS
    (
        SELECT s.UserID
        FROM Signup s
        WHERE s.OrganizationID = @POrgID
          AND s.isdelete = 0
    ),

    -- =========================================================
    -- One live setup per employee - the lowest SetupID
    -- =========================================================
    Setups AS
    (
        SELECT
            tms.UserID,
            tms.StartDay,
            tms.EndDay,
            tms.ContractType,
            DATEPART(HOUR,   tms.MaxTimeinhrs) * 60
          + DATEPART(MINUTE, tms.MaxTimeinhrs)
          + ISNULL(DATEPART(MINUTE, tms.MaxTiminmins), 0) AS DailyMinutes,
            ROW_NUMBER() OVER (PARTITION BY tms.UserID ORDER BY tms.SetupID) AS RowNo
        FROM TimesheetMasterSetup tms
        JOIN Employees e ON e.UserID = tms.UserID
        WHERE ISNULL(tms.IsDelete, 0) = 0
    ),

    -- =========================================================
    -- Each employee's current week and expected time
    -- =========================================================
    EmployeeWeeks AS
    (
        SELECT
            e.UserID,
            st.UserID       AS SetupUserID,   -- NULL = no setup
            st.ContractType,
            wk.WeekStart,
            DATEADD(DAY, 7, wk.WeekStart) AS NextWeekStart,

            -- NULL when the setup is missing or incomplete.
            CASE
                WHEN st.StartDay BETWEEN 1 AND 7 AND st.EndDay BETWEEN 1 AND 7
                    THEN (((st.EndDay - st.StartDay + 7) % 7) + 1) * st.DailyMinutes
            END AS ExpectedMinutes
        FROM Employees e
        LEFT JOIN Setups st
            ON st.UserID = e.UserID
           AND st.RowNo  = 1
        CROSS APPLY
        (
            SELECT DATEADD(
                       DAY,
                       -((@TodayDayID
                          - CASE WHEN st.StartDay BETWEEN 1 AND 7 THEN st.StartDay ELSE 1 END
                          + 7) % 7),
                       @Today) AS WeekStart
        ) wk
    ),

    -- =========================================================
    -- Logged minutes per employee, inside their own week
    -- =========================================================
    Logged AS
    (
        SELECT
            ew.UserID,
            SUM(DATEDIFF(MINUTE,
                    CASE WHEN t.StartAt < b.WeekStartAt     THEN b.WeekStartAt     ELSE t.StartAt END,
                    CASE WHEN t.EndAt   > b.NextWeekStartAt THEN b.NextWeekStartAt ELSE t.EndAt   END)
            ) AS LoggedMinutes
        FROM EmployeeWeeks ew
        JOIN TimeLog tl
            ON tl.UserID = ew.UserID
           AND tl.IsDeleted <> 1
           AND tl.StartDate IS NOT NULL
           AND tl.EndDate   IS NOT NULL
           AND tl.StartTime IS NOT NULL
           AND tl.EndTime   IS NOT NULL
           AND tl.StartDate <  ew.NextWeekStart
           AND tl.EndDate   >= DATEADD(DAY, -1, ew.WeekStart)
        CROSS APPLY
        (
            SELECT
                CAST(ew.WeekStart     AS DATETIME2(0)) AS WeekStartAt,
                CAST(ew.NextWeekStart AS DATETIME2(0)) AS NextWeekStartAt
        ) b
        CROSS APPLY
        (
            SELECT
                DATEADD(SECOND,
                        DATEDIFF(SECOND, CAST('00:00:00' AS TIME), tl.StartTime),
                        CAST(tl.StartDate AS DATETIME2(0))) AS StartAt,
                DATEADD(SECOND,
                        DATEDIFF(SECOND, CAST('00:00:00' AS TIME), tl.EndTime),
                        CAST(tl.EndDate AS DATETIME2(0)))   AS EndAt
        ) t
        WHERE t.EndAt   > t.StartAt
          AND t.EndAt   > b.WeekStartAt
          AND t.StartAt < b.NextWeekStartAt
        GROUP BY ew.UserID
    ),

    -- =========================================================
    -- Organisation totals
    -- =========================================================
    Totals AS
    (
        SELECT
            COUNT(*) AS TotalEmployees,

            ISNULL(SUM(CASE WHEN ew.SetupUserID IS NOT NULL AND ew.ContractType = 1
                            THEN 1 ELSE 0 END), 0) AS TotalFullTimeEmployees,

            ISNULL(SUM(CASE WHEN ew.SetupUserID IS NOT NULL AND ew.ContractType = 2
                            THEN 1 ELSE 0 END), 0) AS TotalPartTimeEmployees,

            ISNULL(SUM(l.LoggedMinutes),    0) AS WeekLoggedMinutes,
            ISNULL(SUM(ew.ExpectedMinutes), 0) AS WeekExpectedMinutes
        FROM EmployeeWeeks ew
        LEFT JOIN Logged l ON l.UserID = ew.UserID
    )

    -- =========================================================
    -- Result
    -- =========================================================
    SELECT
        @POrgID AS OrganizationID,

        t.TotalEmployees,
        t.TotalFullTimeEmployees,
        t.TotalPartTimeEmployees,

        t.WeekLoggedMinutes,
        CONCAT(t.WeekLoggedMinutes / 60, 'h',
               CASE WHEN t.WeekLoggedMinutes % 60 > 0
                    THEN CONCAT(' ', t.WeekLoggedMinutes % 60, 'm') ELSE '' END)
            AS WeekLoggedText,

        t.WeekExpectedMinutes,
        CONCAT(t.WeekExpectedMinutes / 60, 'h',
               CASE WHEN t.WeekExpectedMinutes % 60 > 0
                    THEN CONCAT(' ', t.WeekExpectedMinutes % 60, 'm') ELSE '' END)
            AS WeekExpectedText,

        -- STATIC placeholders - no logic, until an approval workflow exists.
        CAST(0 AS INT) AS PendingTimesheets,
        CAST(0 AS INT) AS ApprovedTimesheets
    FROM Totals t;
END;
GO
