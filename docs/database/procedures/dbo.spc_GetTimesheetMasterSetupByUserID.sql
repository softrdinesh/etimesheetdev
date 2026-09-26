/*
    Procedure: dbo.spc_GetTimesheetMasterSetupByUserID
    Recorded: 2026-09-15
    Revised:  2026-09-21 - body below is the deployed version, as supplied by the
              database owner. Three changes from what was recorded before:
                - JOINs dbo.Signup, and returns s.CountryID
                - returns tms.TimeZone
                - CanUserLoggedPreDayTime is now derived from GETDATE() against
                  tms.TimeEntryLockAt, rather than being selected

    Returns the timesheet limits and working-week settings for one user.

    Called by:
      src/ETimeSheet.Infrastructure/Repositories/TimeLogRepository.cs
    Result set mapped by:
      src/ETimeSheet.Application/Models/TimeLog.cs (TimesheetMasterSetupDetail)
      src/ETimeSheet.Infrastructure/Data/Context.cs (ConfigureStoredProcedureResults)

    NOTE: two columns are returned under an alias. The keyless result type binds
    to the ALIAS, not the underlying column, so renaming an alias here breaks the
    mapping:
        MaxTimeinhrs -> MaxTimeLoggedByUserInHours
        MaxTiminmins -> MaxTimeLoggedByUserInMinutes

    CanUserLoggedPreDayTime is NOT a column on dbo.TimesheetMasterSetup - a
    direct SELECT of it against the table fails with "Invalid column name". The
    procedure derives it, which is why it is mapped only on this procedure's
    keyless result type and is absent from the table entity and from the Admin
    CRUD contract. It comes back as an int holding 0 or 1, not a bit, which is
    why the mapping needs .HasConversion<int>().

    THREE THINGS THE 2026-09-21 BODY IMPLIES, worth knowing before relying on it:

      1. CanUserLoggedPreDayTime is measured on the DATABASE SERVER's clock.
         GETDATE() is the SQL Server's local time, so this flag answers "has the
         cut-off passed where the server is", not "where the employee is". The
         API does not use it: TimeLogService judges TimeEntryLockAt itself, in
         the employee's zone, via EmployeeClock.

      2. A NULL TimeEntryLockAt yields 0, not 1. The comparison against NULL is
         UNKNOWN, so the CASE falls to ELSE. A setup that has never had a
         cut-off configured therefore reports "may not log pre-day time". The
         save does not use this flag: it reads TimeEntryLockAt itself, and a
         NULL there means no lock at all.

      3. The JOIN to dbo.Signup is an INNER JOIN, so a setup whose UserID has no
         matching Signup row returns NO ROWS at all, exactly as if the user had
         no setup.

    CountryID is dbo.Signup's, not dbo.TimesheetMasterSetup's. The setup table
    has a CountryID column of its own - the one the Admin save writes and
    resolves the time zone against - and the two can hold different values.

    The table itself is recorded at ../schema/dbo.TimesheetMasterSetup.sql.
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_GetTimesheetMasterSetupByUserID]
    @PUserID INT
AS
BEGIN

    SELECT
        tms.SetupID,
        tms.MaxTimeinhrs AS MaxTimeLoggedByUserInHours,
        tms.MaxTiminmins AS MaxTimeLoggedByUserInMinutes,
        tms.ContractType,
        tms.StartDay,
        tms.EndDay,
        CASE
            WHEN CAST(GETDATE() AS TIME) <= tms.TimeEntryLockAt THEN 1
            ELSE 0
        END AS CanUserLoggedPreDayTime,
        s.CountryID,
        tms.TimeZone
    FROM
        TimesheetMasterSetup tms
        JOIN Signup s ON s.UserID = tms.UserID
    WHERE
        s.UserID = @PUserID;

END;
GO
