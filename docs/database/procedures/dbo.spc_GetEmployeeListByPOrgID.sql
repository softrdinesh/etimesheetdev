/*
    Procedure: dbo.spc_GetEmployeeListByPOrgID
    Recorded: 2026-09-19 (body as handed over by the database owner)

    Every employee in one organisation, with their contracted time per week,
    what they have logged in the CURRENT Monday-Sunday week, and how far through
    the contracted week that puts them.

    Called by:
      src/ETimeSheet.Infrastructure/Repositories/AdminRepository.cs
    Result set mapped by:
      src/ETimeSheet.Application/Models/Admin.cs (EmployeeListDetail)
      src/ETimeSheet.Infrastructure/Data/Context.cs (ConfigureStoredProcedureResults)
    Served by:
      GET /api/v1/Admin/get-all-employees-by-orgid/{orgID}

    Recorded as CREATE OR ALTER: it was handed over as ALTER PROCEDURE, which
    cannot run against the empty database the integration fixture builds.

    Things worth knowing before changing this:

      - "Employee" is Signup.RoleID = 2, decided here and nowhere else. That
        does NOT agree with ETimeSheet.Shared.Enums.RoleType, where 2 is Manager.
        The two vocabularies genuinely differ; the API does not reconcile them,
        it takes whatever rows this returns.

      - LEFT JOIN to TimesheetMasterSetup, so an employee with NO setup is still
        returned, with SetupID null and every expected-time column null. The
        endpoint's summary counts those as "without setup" - it is the reason
        the join is not an INNER one, so do not tighten it.

      - It is not guarded against a user holding more than one setup row: the
        LEFT JOIN would return that employee twice and the head count would
        double-count them. The Admin save path makes a second row impossible,
        but no database constraint does.

      - The week is derived from GETDATE() inside the procedure, so it always
        means "this week on the server". It cannot be substituted by the test
        clock - an integration test has to arrange its rows relative to the real
        current week.

      - '19000101' is a Monday, which is what makes the % 7 arithmetic land on
        Monday. Changing the anchor date silently shifts the whole week.

      - ExpectedHoursPerWeek / ExpectedMinsPerWeek are the quotient and the
        REMAINDER of one division, not two independent figures: 37h30 a week
        comes back as 37 and 30.

      - The expected-minutes expression takes DATEPART(MINUTE, ...) of
        MaxTiminmins but ignores DATEPART(HOUR, ...) of it. That is recorded as
        handed over. If MaxTiminmins ever holds an hour component, it is
        dropped here.

      - ProgressOnThisWeek is clamped to 100 by the procedure, so a client can
        draw a bar straight from it. Its CASE mixes int literals with a
        DECIMAL(10, 0) cast, so SQL type precedence makes the whole column
        decimal - which is why EmployeeListDetail.ProgressOnThisWeek is a
        decimal and not an int.

    The tables are recorded at:
      ../schema/dbo.Signup.sql               (PARTIAL - see the note in that file)
      ../schema/dbo.TimesheetMasterSetup.sql
      ../schema/dbo.TimeLog.sql
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_GetEmployeeListByPOrgID]
(
    @POrgID INT
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Today DATE = CAST(GETDATE() AS DATE);

    -- =========================================================
    -- Monday of current week
    -- =========================================================
    DECLARE @WeekStart DATE =
        DATEADD(
            DAY,
            -(DATEDIFF(DAY, '19000101', @Today) % 7),
            @Today
        );

    -- =========================================================
    -- Monday of next week
    -- =========================================================
    DECLARE @NextWeekStart DATE =
        DATEADD(DAY, 7, @WeekStart);


    SELECT 
        s.UserID,
        tms.SetupID,
        s.Name,
        s.Email,

        -- =====================================================
        -- Expected weekly hours
        -- =====================================================
        CASE 
            WHEN weekly.TotalMinutes IS NOT NULL
                THEN weekly.TotalMinutes / 60
            ELSE NULL
        END AS ExpectedHoursPerWeek,

        -- Remaining minutes per week
        CASE 
            WHEN weekly.TotalMinutes IS NOT NULL
                THEN weekly.TotalMinutes % 60
            ELSE NULL
        END AS ExpectedMinsPerWeek,

        -- Expected working time text
        CASE 
            WHEN weekly.TotalMinutes IS NULL
                THEN NULL
            ELSE CONCAT(
                weekly.TotalMinutes / 60,
                'h',
                CASE 
                    WHEN weekly.TotalMinutes % 60 > 0
                        THEN CONCAT(
                            ' ',
                            weekly.TotalMinutes % 60,
                            'm'
                        )
                    ELSE ''
                END,
                '/week'
            )
        END AS ExpectedHoursPerWeekText,

        -- =====================================================
        -- Current week logged time
        -- =====================================================
        ISNULL(logged.TotalLoggedMinutes, 0) / 60 AS TotalLoggedHoursCurrentWeek,

        ISNULL(logged.TotalLoggedMinutes, 0) % 60 AS TotalLoggedMinsCurrentWeek,

        CONCAT(
            ISNULL(logged.TotalLoggedMinutes, 0) / 60,
            'h',
            CASE 
                WHEN ISNULL(logged.TotalLoggedMinutes, 0) % 60 > 0
                    THEN CONCAT(
                        ' ',
                        ISNULL(logged.TotalLoggedMinutes, 0) % 60,
                        'm'
                    )
                ELSE ''
            END
        ) AS TotalLoggedHoursCurrentWeekText,

        -- =====================================================
        -- Progress on current week
        -- Maximum = 100%
        -- =====================================================
        CASE
            WHEN weekly.TotalMinutes IS NULL
                OR weekly.TotalMinutes <= 0
                THEN 0

            WHEN ISNULL(logged.TotalLoggedMinutes, 0) >= weekly.TotalMinutes
                THEN 100

            ELSE
                CAST(
                    ISNULL(logged.TotalLoggedMinutes, 0) * 100.0
                    / weekly.TotalMinutes
                    AS DECIMAL(10, 0)
                )
        END AS ProgressOnThisWeek,

        -- =====================================================
        -- Contract type
        -- =====================================================
        tms.ContractType AS ContractTypeID,

        CASE 
            WHEN tms.ContractType = 1 THEN 'Full Time'
            WHEN tms.ContractType = 2 THEN 'Part Time'
        END AS ContractType

    FROM Signup s

    LEFT JOIN TimesheetMasterSetup tms 
        ON tms.UserID = s.UserID

    -- =========================================================
    -- Calculate expected weekly working time
    -- =========================================================
    CROSS APPLY
    (
        SELECT
            CASE
                WHEN tms.StartDay IS NULL
                  OR tms.EndDay IS NULL
                  OR tms.MaxTimeinhrs IS NULL
                  OR tms.MaxTiminmins IS NULL
                  OR tms.EndDay < tms.StartDay
                THEN NULL

                ELSE
                    (tms.EndDay - tms.StartDay + 1)
                    *
                    (
                        (DATEPART(HOUR, tms.MaxTimeinhrs) * 60)
                        + DATEPART(MINUTE, tms.MaxTimeinhrs)
                        + DATEPART(MINUTE, tms.MaxTiminmins)
                    )
            END AS TotalMinutes
    ) weekly

    -- =========================================================
    -- Calculate total logged time for current Monday-Sunday week
    -- =========================================================
    OUTER APPLY
    (
        SELECT
            SUM(
                DATEDIFF(
                    MINUTE,
                    CASE
                        WHEN logdata.StartDateTime <
                             CAST(@WeekStart AS DATETIME2)
                            THEN CAST(@WeekStart AS DATETIME2)
                        ELSE logdata.StartDateTime
                    END,
                    CASE
                        WHEN logdata.EndDateTime >
                             CAST(@NextWeekStart AS DATETIME2)
                            THEN CAST(@NextWeekStart AS DATETIME2)
                        ELSE logdata.EndDateTime
                    END
                )
            ) AS TotalLoggedMinutes

        FROM
        (
            SELECT
                DATEADD(
                    SECOND,
                    DATEDIFF(
                        SECOND,
                        CAST('00:00:00' AS TIME),
                        tl.StartTime
                    ),
                    CAST(tl.StartDate AS DATETIME2)
                ) AS StartDateTime,

                DATEADD(
                    SECOND,
                    DATEDIFF(
                        SECOND,
                        CAST('00:00:00' AS TIME),
                        tl.EndTime
                    ),
                    CAST(tl.EndDate AS DATETIME2)
                ) AS EndDateTime

            FROM TimeLog tl

            WHERE tl.UserID = s.UserID

                -- Log overlaps current Monday-Sunday week
                AND tl.StartDate < @NextWeekStart
                AND tl.EndDate >= @WeekStart

                -- Ignore incomplete records
                AND tl.StartDate IS NOT NULL
                AND tl.EndDate IS NOT NULL
                AND tl.StartTime IS NOT NULL
                AND tl.EndTime IS NOT NULL
        ) logdata

        -- Only count valid durations
        WHERE logdata.EndDateTime > logdata.StartDateTime

    ) logged

    -- =========================================================
    -- Employee filter
    -- =========================================================
    WHERE s.RoleID = 2
      AND s.OrganizationID = @POrgID;

END;
GO
