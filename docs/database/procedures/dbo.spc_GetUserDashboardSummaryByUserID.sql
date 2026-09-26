/*
    Procedure: dbo.spc_GetUserDashboardSummaryByUserID
    Recorded: 2026-09-27 - body below is the deployed version, as supplied by
              the database owner. Handed over as ALTER PROCEDURE; recorded as
              CREATE OR ALTER so it runs against the empty database the
              integration fixture builds.

    Returns one row of dashboard figures for one user:
      - today's logged time
      - this week's logged time
      - this week's expected time
      - logged / expected as a percentage
      - time still pending to reach the expected figure

    Called by:
      src/ETimeSheet.Infrastructure/Repositories/TimeLogRepository.cs
      (GET /api/v1/TimeLog/get-user-dashboard-summary-by-userID/{userId})
    Result set mapped by:
      src/ETimeSheet.Application/Models/TimeLog.cs (UserDashboardSummaryDetail)
      src/ETimeSheet.Infrastructure/Data/Context.cs (ConfigureStoredProcedureResults)

    Parameters:
      @PUserID  the user. The only parameter.

    "Today" is the DATABASE SERVER's date - CAST(GETDATE() AS DATE). An
    earlier draft took an optional @PToday so the API could pass the
    employee's local date; the deployed version does not. For anyone outside
    the server's zone, "today" (and, on the week's first day, "this week")
    is wrong for part of every day. It also cannot be substituted by the
    test clock: an integration test has to arrange its rows relative to the
    real current date.

    The week:
      Anchored on TimesheetMasterSetup.StartDay (dbo.DayMaster.DayID,
      1 = Monday ... 7 = Sunday) and seven calendar days long - the same
      week the save uses to share one SheetCode. A Monday-to-Friday setup
      counts Saturday and Sunday entries too. No usable StartDay falls back
      to a Monday week.

    Expected time:
      working days in StartDay..EndDay (wrapping past Sunday, so a
      Sunday-to-Thursday week is 5 days)
      x daily time (MaxTimeinhrs hours + minutes, plus the minutes of
      MaxTiminmins). NULL when the user has no setup, or the setup is
      missing any of those values. Exceptionday is NOT counted as expected
      time.

    Logged time:
      Every live entry (IsDeleted <> 1), Save and Draft alike. Incomplete
      rows (any date or time NULL) and rows that do not run forwards are
      skipped. An entry crossing a boundary counts only the part inside it.

    Result:
      One row, or NO rows when the user is not in dbo.Signup or is deleted
      there. A user with no timesheet setup still gets a row, with the
      expected / percentage / pending figures NULL.

    The SELECT list is a contract: the keyless result type binds by column
    name, so adding, removing or renaming a column here requires the
    matching change to UserDashboardSummaryDetail and
    UserDashboardSummaryResponse.

    The tables are recorded at:
      ../schema/dbo.TimesheetMasterSetup.sql
      ../schema/dbo.TimeLog.sql
      ../schema/dbo.Signup.sql  (PARTIAL - listed in ../README.md)
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_GetUserDashboardSummaryByUserID]
        @PUserID INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Today    DATE = CAST(GETDATE() AS DATE);
    DECLARE @Tomorrow DATE = DATEADD(DAY, 1, @Today);

    -- DayMaster numbering: 1 = Monday ... 7 = Sunday. '19000101' is a
    -- Monday, so the % 7 is 0 on a Monday whatever DATEFIRST is set to.
    DECLARE @TodayDayID INT = (DATEDIFF(DAY, '19000101', @Today) % 7) + 1;

    -- =========================================================
    -- The user's setup
    -- =========================================================
    DECLARE @StartDay     INT;
    DECLARE @EndDay       INT;
    DECLARE @DailyMinutes INT;

    SELECT TOP (1)
        @StartDay     = tms.StartDay,
        @EndDay       = tms.EndDay,
        @DailyMinutes = DATEPART(HOUR,   tms.MaxTimeinhrs) * 60
                      + DATEPART(MINUTE, tms.MaxTimeinhrs)
                      + ISNULL(DATEPART(MINUTE, tms.MaxTiminmins), 0)
    FROM TimesheetMasterSetup tms
    WHERE tms.UserID = @PUserID
      AND ISNULL(tms.IsDelete, 0) = 0
    ORDER BY tms.SetupID;

    -- =========================================================
    -- The current timesheet week
    -- =========================================================
    DECLARE @WeekStartDay INT = CASE WHEN @StartDay BETWEEN 1 AND 7 THEN @StartDay ELSE 1 END;

    DECLARE @WeekStart DATE = DATEADD(DAY, -((@TodayDayID - @WeekStartDay + 7) % 7), @Today);

    DECLARE @NextWeekStart DATE = DATEADD(DAY, 7, @WeekStart);

    -- Working days StartDay..EndDay inclusive, wrapping past Sunday.
    DECLARE @WorkingDays INT =
        CASE
            WHEN @StartDay BETWEEN 1 AND 7 AND @EndDay BETWEEN 1 AND 7
                THEN ((@EndDay - @StartDay + 7) % 7) + 1
        END;

    -- NULL when either side is NULL - no setup, or an incomplete one.
    DECLARE @ExpectedMinutes INT = @WorkingDays * @DailyMinutes;

    -- =========================================================
    -- Logged time: today, and this week
    -- =========================================================
    DECLARE @TodayStartAt    DATETIME2(0) = CAST(@Today         AS DATETIME2(0));
    DECLARE @TomorrowStartAt DATETIME2(0) = CAST(@Tomorrow      AS DATETIME2(0));
    DECLARE @WeekStartAt     DATETIME2(0) = CAST(@WeekStart     AS DATETIME2(0));
    DECLARE @NextWeekStartAt DATETIME2(0) = CAST(@NextWeekStart AS DATETIME2(0));

    DECLARE @TodayLoggedMinutes INT;
    DECLARE @WeekLoggedMinutes  INT;

    ;WITH Logs AS
    (
        SELECT
            DATEADD(SECOND,
                    DATEDIFF(SECOND, CAST('00:00:00' AS TIME), tl.StartTime),
                    CAST(tl.StartDate AS DATETIME2(0))) AS StartAt,
            DATEADD(SECOND,
                    DATEDIFF(SECOND, CAST('00:00:00' AS TIME), tl.EndTime),
                    CAST(tl.EndDate AS DATETIME2(0)))   AS EndAt
        FROM TimeLog tl
        WHERE tl.UserID = @PUserID
          AND tl.IsDeleted <> 1
          AND tl.StartDate IS NOT NULL
          AND tl.EndDate   IS NOT NULL
          AND tl.StartTime IS NOT NULL
          AND tl.EndTime   IS NOT NULL
          -- Overlaps the week. Today is always inside the week, so this
          -- also covers every entry that can touch today.
          AND tl.StartDate <  @NextWeekStart
          AND tl.EndDate   >= DATEADD(DAY, -1, @WeekStart)
    )
    SELECT
        @WeekLoggedMinutes = ISNULL(SUM(
            CASE
                WHEN l.EndAt > @WeekStartAt AND l.StartAt < @NextWeekStartAt
                    THEN DATEDIFF(MINUTE,
                             CASE WHEN l.StartAt < @WeekStartAt     THEN @WeekStartAt     ELSE l.StartAt END,
                             CASE WHEN l.EndAt   > @NextWeekStartAt THEN @NextWeekStartAt ELSE l.EndAt   END)
                ELSE 0
            END), 0),

        @TodayLoggedMinutes = ISNULL(SUM(
            CASE
                WHEN l.EndAt > @TodayStartAt AND l.StartAt < @TomorrowStartAt
                    THEN DATEDIFF(MINUTE,
                             CASE WHEN l.StartAt < @TodayStartAt    THEN @TodayStartAt    ELSE l.StartAt END,
                             CASE WHEN l.EndAt   > @TomorrowStartAt THEN @TomorrowStartAt ELSE l.EndAt   END)
                ELSE 0
            END), 0)
    FROM Logs l
    WHERE l.EndAt > l.StartAt;

    -- =========================================================
    -- Result
    -- =========================================================
    SELECT
        s.UserID,
        s.Name,

        @WeekStart                    AS WeekStartDate,
        DATEADD(DAY, -1, @NextWeekStart) AS WeekEndDate,

        -- Today
        @TodayLoggedMinutes AS TodayLoggedMinutes,
        CONCAT(@TodayLoggedMinutes / 60, 'h',
               CASE WHEN @TodayLoggedMinutes % 60 > 0
                    THEN CONCAT(' ', @TodayLoggedMinutes % 60, 'm') ELSE '' END)
            AS TodayLoggedText,

        -- This week, logged
        @WeekLoggedMinutes AS WeekLoggedMinutes,
        CONCAT(@WeekLoggedMinutes / 60, 'h',
               CASE WHEN @WeekLoggedMinutes % 60 > 0
                    THEN CONCAT(' ', @WeekLoggedMinutes % 60, 'm') ELSE '' END)
            AS WeekLoggedText,

        -- This week, expected
        @ExpectedMinutes AS WeekExpectedMinutes,
        CASE WHEN @ExpectedMinutes IS NULL THEN NULL
             ELSE CONCAT(@ExpectedMinutes / 60, 'h',
                         CASE WHEN @ExpectedMinutes % 60 > 0
                              THEN CONCAT(' ', @ExpectedMinutes % 60, 'm') ELSE '' END)
        END AS WeekExpectedText,

        -- Logged as a percentage of expected, capped at 100
        CASE
            WHEN @ExpectedMinutes IS NULL OR @ExpectedMinutes <= 0 THEN NULL
            WHEN @WeekLoggedMinutes >= @ExpectedMinutes            THEN CAST(100 AS DECIMAL(5, 2))
            ELSE CAST(ROUND(@WeekLoggedMinutes * 100.0 / @ExpectedMinutes, 2) AS DECIMAL(5, 2))
        END AS WeekLoggedPercentage,

        -- Still to log this week, never below zero
        pending.PendingMinutes AS WeekPendingMinutes,
        CASE WHEN pending.PendingMinutes IS NULL THEN NULL
             ELSE CONCAT(pending.PendingMinutes / 60, 'h',
                         CASE WHEN pending.PendingMinutes % 60 > 0
                              THEN CONCAT(' ', pending.PendingMinutes % 60, 'm') ELSE '' END)
        END AS WeekPendingText

    FROM Signup s
    CROSS APPLY
    (
        SELECT CASE
                   WHEN @ExpectedMinutes IS NULL                THEN NULL
                   WHEN @WeekLoggedMinutes >= @ExpectedMinutes  THEN 0
                   ELSE @ExpectedMinutes - @WeekLoggedMinutes
               END AS PendingMinutes
    ) pending
    WHERE s.UserID = @PUserID
      AND s.isdelete = 0;
END;
GO
