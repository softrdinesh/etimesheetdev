/*
    Procedure: dbo.spc_GetTimesheetMasterSetupByUserID
    Recorded: 2026-09-15 (SELECT list last changed 2026-09-15: TimeEntryLockAt
    removed, CanUserLoggedPreDayTime added)

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
    deployed procedure must therefore derive it (the body recorded below is what
    was handed over, so the two have drifted). Because of that it is mapped only
    on this procedure's keyless result type and is absent from the table entity
    and from the Admin CRUD contract. It comes back as an int holding 0 or
    1, not a bit, which is why the mapping needs .HasConversion<int>().

    The table itself is recorded at ../schema/dbo.TimesheetMasterSetup.sql.
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_GetTimesheetMasterSetupByUserID]
    @PUserID INT
AS
BEGIN

    SELECT
        SetupID,
        MaxTimeinhrs AS MaxTimeLoggedByUserInHours,
        MaxTiminmins AS MaxTimeLoggedByUserInMinutes,
        ContractType,
        StartDay,
        EndDay,
        CanUserLoggedPreDayTime
    FROM
        TimesheetMasterSetup
    WHERE
        UserID = @PUserID;

END;
GO
