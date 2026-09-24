/*
    Procedure: dbo.spc_GetTimeLoggedDetailsForTask
    Recorded: 2026-09-14 (date-range parameters added 2026-09-15;
    TotalWorkingHours / TotalWorkingMinutes added 2026-09-15)
    Revised:  2026-09-24 - IsProjectTask added to the SELECT.
    Revised:  2026-09-25 - reshaped by the database owner; body below is the
              deployed version:
                - @PTaskID, @PStartDate and @PEndData REMOVED. The only
                  parameter is @PUserID, so it returns every live entry the
                  user has, across all tasks and all dates.
                - TaskID added to the SELECT, before IsProjectTask; both sit
                  after TotalWorkingMinutes.
                - ORDER BY CreateDate DESC - most recently LOGGED first, not
                  most recent StartDate.

    The name still says "ForTask", but since 2026-09-25 it no longer filters
    by task. The name is the database's, and TimeLogRepository must call it
    by that name.

    TotalWorkingHours and TotalWorkingMinutes are COMPUTED by the procedure -
    they are not columns on dbo.TimeLog. Both are null whenever any of the
    four date/time values behind DATEDIFF is null.

    No row limit: a user's whole history comes back in one call.

    Called by:
      src/ETimeSheet.Infrastructure/Repositories/TimeLogRepository.cs
      (GET /api/v1/TimeLog/get-logged-time-list/{userId})
    Result set mapped by:
      src/ETimeSheet.Application/Models/TimeLog.cs (TimeLoggedDetail)
      src/ETimeSheet.Infrastructure/Data/Context.cs (ConfigureStoredProcedureResults)

    The SELECT list is a contract: the keyless result type binds by column name,
    so adding, removing or renaming a column here requires the matching change
    to TimeLoggedDetail and TimeLoggedDetailResponse.
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_GetTimeLoggedDetailsForTask]
    @PUserID INT
AS
BEGIN
    SELECT
        SheetID,
        SheetCode,
        Description,
        StartDate,
        StartTime,
        EndDate,
        EndTime,
        Status,

        -- Total working time in hours
        ROUND(
            DATEDIFF(
                MINUTE,
                CAST(StartDate AS DATETIME) + CAST(StartTime AS DATETIME),
                CAST(EndDate AS DATETIME) + CAST(EndTime AS DATETIME)
            ) / 60.0, 2) AS TotalWorkingHours,

        -- Total working time in minutes
        DATEDIFF(
            MINUTE,
            CAST(StartDate AS DATETIME) + CAST(StartTime AS DATETIME),
            CAST(EndDate AS DATETIME) + CAST(EndTime AS DATETIME)
        ) AS TotalWorkingMinutes,

        TaskID,
        IsProjectTask
    FROM TimeLog
    WHERE
        UserID = @PUserID
        AND IsDeleted <> 1
    ORDER BY CreateDate DESC;
END;
GO
