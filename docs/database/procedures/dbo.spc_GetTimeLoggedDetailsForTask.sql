/*
    Procedure: dbo.spc_GetTimeLoggedDetailsForTask
    Recorded: 2026-09-14 (date-range parameters added 2026-09-15;
    TotalWorkingHours / TotalWorkingMinutes added 2026-09-15)

    The last two columns are COMPUTED by the procedure - they are not columns on
    dbo.TimeLog. Both are null whenever any of the four date/time values behind
    DATEDIFF is null.

    NOTE THE SPELLING: the fourth parameter is @PEndData, not @PEndDate. The
    typo is in the database, and TimeLogRepository must pass that exact name.

    Returns every entry one user logged against one task.

    Called by:
      src/ETimeSheet.Infrastructure/Repositories/TimeLogRepository.cs
    Result set mapped by:
      src/ETimeSheet.Application/Models/Results/TimeLoggedDetail.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/TimeLoggedDetailConfiguration.cs

    The SELECT list is a contract: the keyless result type binds by column name,
    so adding, removing or renaming a column here requires the matching change
    to TimeLoggedDetail and TimeLoggedDetailResponse.
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_GetTimeLoggedDetailsForTask]
    @PUserID INT,
    @PTaskID INT,
    @PStartDate DATE,
    @PEndData DATE
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
        ) AS TotalWorkingMinutes
    FROM TimeLog
    WHERE
        UserID = @PUserID
        AND TaskID = @PTaskID
        AND IsDeleted <> 1
        AND StartDate >= @PStartDate
        AND StartDate <= @PEndData;
END;
GO
