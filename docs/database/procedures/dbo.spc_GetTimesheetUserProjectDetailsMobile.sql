/*
    Procedure: dbo.spc_GetTimesheetUserProjectDetailsMobile
    Status:    NOT YET APPLIED - the owner runs it by hand. Change this line to
               "Applied: <date>" once it has been run.
    Recorded:  2026-11-19 - new procedure.
    Revised:   2026-11-19 - NOT YET APPLIED: @POrgID added (required) and
               @PProjectID, @PFromDate, @PToDate, @PFromUtc, @PToUtc added
               (optional - NULL is "not applied"). @PLogDate, @PDayStartUtc and
               @PDayEndUtc dropped: the procedure no longer reads one day, it
               returns every selfie session and every day's logged hours in
               the range, and the API groups them by day. RowType,
               TimesheetUserProjectSetupID and AttendanceID added to the result.

    Returns one timesheet user's project assignments in one organisation,
    with their selfie check-ins / check-outs and logged hours - for the mobile
    app, through
    GET api/v1/TimesheetProject/get-user-project-details-mobile.

    Parameters:
      @PUserID     REQUIRED. The user (dbo.Signup.UserID).
      @POrgID      REQUIRED. The organisation - only assignments to projects
                   of this organisation (dbo.TimesheetProject.OrganizationID).
      @PProjectID  Optional. One project; NULL is every project.
      @PFromDate   Optional. First time log date (dbo.TimesheetProjectTimeLog
                   .LogDate) to include; NULL is no lower bound.
      @PToDate     Optional. Last time log date to include, inclusive; NULL is
                   no upper bound.
      @PFromUtc    Optional. @PFromDate's first instant in the user's time
                   zone, in UTC, inclusive - the bound for the selfie
                   sessions, whose columns are UTC. NULL is no lower bound.
      @PToUtc      Optional. The first instant of the day AFTER @PToDate in the
                   user's time zone, in UTC, EXCLUSIVE. NULL is no upper bound.
                   The API works out both UTC bounds, so the procedure does no
                   time zone arithmetic of its own.

    Result: ONE result set of three kinds of row, told apart by RowType.
      Every row carries the user and the assignment:
        RowType, UserID, Name, Email, OrganizationID, OrganizationName,
        TimesheetUserProjectSetupID, ProjectID, ProjectName.

      RowType 'Assignment' - exactly one per live assignment, so a project
        with no activity in the range is still returned. Every other column
        NULL.
      RowType 'Attendance' - one per dbo.TimesheetUserSelfieAttendance session
        of the user on the project checked in within @PFromUtc / @PToUtc:
          AttendanceID, CheckInDateTimeUTC, CheckInSelfiePath,
          CheckOutDateTimeUTC, CheckOutSelfiePath (the check-out pair NULL
          while the session is open). The selfie paths are the pre-signed
          URLs the document service answered the upload with.
      RowType 'TimeLog' - one per date the user logged live time on the
        project within @PFromDate / @PToDate:
          TimeLogDate   the LogDate.
          TimeLogHours  the sum of that date's live HoursLogged - every entry,
                        by selfie or by add-edit-timesheet-project-time-log.

      The API puts a session on the user's own date of its check-in, in their
      time zone, matches it to that date's 'TimeLog' row, and keeps the latest
      session of each date.

      NO ROWS means the user has no live assignment on a project of the
      organisation (and project, when given) - including a missing or deleted
      user.

    Live rows only:
      - The user: dbo.Signup.IsDelete = 0.
      - The assignment: ISNULL(IsDelete, 0) = 0.
      - Time log: IsDelete <> 1.
      - dbo.TimesheetUserSelfieAttendance has no soft-delete column.

    Order:
      By project name, then TimesheetUserProjectSetupID; the API orders the
      days itself.

    The tables are recorded at:
      ../schema/dbo.Timesheet_Users_Project_setup.sql
      ../schema/dbo.TimesheetProject.sql
      ../schema/dbo.TimesheetUserSelfieAttendance.sql
      ../schema/dbo.TimesheetProjectTimeLog.sql
    NOT RECORDED - dbo.Signup and dbo.Organization; the test container builds
    them from the EF mapping.
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_GetTimesheetUserProjectDetailsMobile]
    @PUserID     INT,
    @POrgID      INT,
    @PProjectID  INT           = NULL,
    @PFromDate   DATE          = NULL,
    @PToDate     DATE          = NULL,
    @PFromUtc    DATETIME2(0)  = NULL,
    @PToUtc      DATETIME2(0)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- The user's live assignments on projects of the organisation.
    WITH assignment AS
    (
        SELECT
            s.UserID                            AS UserID,
            s.Name                              AS Name,
            s.Email                             AS Email,
            tp.OrganizationID                   AS OrganizationID,
            o.OrganizationName                  AS OrganizationName,
            tups.TimesheetUserProjectSetupID    AS TimesheetUserProjectSetupID,
            tups.ProjectID                      AS ProjectID,
            tp.Projectname                      AS ProjectName
        FROM dbo.Timesheet_Users_Project_setup tups
        -- INNER, so a deleted user's assignments are not returned either.
        INNER JOIN dbo.Signup s
            ON s.UserID = tups.UserID
           AND s.IsDelete = 0
        -- INNER: the organisation is the project's, so an assignment whose
        -- project is missing belongs to no organisation and is not returned.
        INNER JOIN dbo.TimesheetProject tp
            ON tp.ProjectID = tups.ProjectID
        LEFT JOIN dbo.Organization o
            ON o.OrganizationID = tp.OrganizationID
        WHERE tups.UserID = @PUserID
          AND tp.OrganizationID = @POrgID
          AND (@PProjectID IS NULL OR tups.ProjectID = @PProjectID)
          AND ISNULL(tups.IsDelete, 0) = 0
    )
    SELECT *
    FROM
    (
        -- One row per assignment, whatever its activity.
        SELECT
            N'Assignment'                       AS RowType,
            a.UserID, a.Name, a.Email, a.OrganizationID, a.OrganizationName,
            a.TimesheetUserProjectSetupID, a.ProjectID, a.ProjectName,
            CAST(NULL AS BIGINT)                AS AttendanceID,
            CAST(NULL AS DATETIME2(0))          AS CheckInDateTimeUTC,
            CAST(NULL AS NVARCHAR(500))         AS CheckInSelfiePath,
            CAST(NULL AS DATETIME2(0))          AS CheckOutDateTimeUTC,
            CAST(NULL AS NVARCHAR(500))         AS CheckOutSelfiePath,
            CAST(NULL AS DATE)                  AS TimeLogDate,
            CAST(NULL AS INT)                   AS TimeLogHours
        FROM assignment a

        UNION ALL

        -- Every selfie session in the range.
        SELECT
            N'Attendance',
            a.UserID, a.Name, a.Email, a.OrganizationID, a.OrganizationName,
            a.TimesheetUserProjectSetupID, a.ProjectID, a.ProjectName,
            sa.AttendanceID,
            sa.CheckInDateTimeUTC,
            sa.CheckInSelfiePath,
            sa.CheckOutDateTimeUTC,
            sa.CheckOutSelfiePath,
            NULL,
            NULL
        FROM assignment a
        INNER JOIN dbo.TimesheetUserSelfieAttendance sa
            ON sa.UserID = a.UserID
           AND sa.ProjectID = a.ProjectID
        WHERE (@PFromUtc IS NULL OR sa.CheckInDateTimeUTC >= @PFromUtc)
          AND (@PToUtc   IS NULL OR sa.CheckInDateTimeUTC <  @PToUtc)

        UNION ALL

        -- Every day's live hours in the range.
        SELECT
            N'TimeLog',
            a.UserID, a.Name, a.Email, a.OrganizationID, a.OrganizationName,
            a.TimesheetUserProjectSetupID, a.ProjectID, a.ProjectName,
            NULL, NULL, NULL, NULL, NULL,
            tl.LogDate,
            SUM(tl.HoursLogged)
        FROM assignment a
        INNER JOIN dbo.TimesheetProjectTimeLog tl
            ON tl.UserID = a.UserID
           AND tl.ProjectID = a.ProjectID
        WHERE tl.IsDelete <> 1
          AND (@PFromDate IS NULL OR tl.LogDate >= @PFromDate)
          AND (@PToDate   IS NULL OR tl.LogDate <= @PToDate)
        GROUP BY
            a.UserID, a.Name, a.Email, a.OrganizationID, a.OrganizationName,
            a.TimesheetUserProjectSetupID, a.ProjectID, a.ProjectName,
            tl.LogDate
    ) result
    ORDER BY result.ProjectName, result.TimesheetUserProjectSetupID;
END
GO
