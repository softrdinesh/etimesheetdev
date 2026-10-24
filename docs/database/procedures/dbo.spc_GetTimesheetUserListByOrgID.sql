/*
    Procedure: dbo.spc_GetTimesheetUserListByOrgID
    Status:    DRAFT for review - not applied to any database.
    Recorded:  2026-10-17 - DRAFT: new procedure.
    Revised:   2026-10-18 - DRAFT: MaxLogHoursPerDay added, for the
               Max_log_hour_day column the owner added to the table.
    Revised:   2026-10-23 - DRAFT, to be applied by the owner: CountryID,
               CountryName, Address, CurrencyID, CurrencyName and
               CurrencySymbol added as the last columns. The API reads them,
               so get-timesheet-user-by-orgID fails until this is applied.
    Revised:   2026-10-25 - reads dbo.CountryCurrency_Lookup (table renamed from dbo.CountryCurrency).

    Returns the timesheet users of one organisation - every live project
    assignment in dbo.Timesheet_Users_Project_setup of every live user of the
    organisation - for
    GET api/v1/TimesheetProject/get-timesheet-user-by-orgID/{orgID}.

    Parameters:
      @POrgID  the organisation (dbo.Signup.OrganizationID).

    Organisation:
      Taken from the USER - dbo.Signup.OrganizationID - because
      dbo.Timesheet_Users_Project_setup has no organisation column.

    Live rows only:
      - The assignment: ISNULL(IsDelete, 0) = 0 - the API's soft delete
        writes 1, spc_Signup_timesheet_user writes 0.
      - The user: IsDelete = 0. Every other procedure on dbo.Signup treats
        only 0 as live, so a NULL is not live here either.

    Result columns:
      TimesheetUserProjectSetupID, UserID, Name, Email, OrganizationID,
      ProjectID, ProjectCode (from dbo.TimesheetProject.Code), ProjectName
      (aliased from Projectname), ProjectRoleID, ProjectRoleName (aliased
      from dbo.TimesheetUserRole.Rolename), JobType (aliased from Jobtype),
      CostRate (aliased from Cost_rate_per_hour), BillRate (aliased from
      Billable_rate_per_hour), WorkLocationRange (aliased from
      WorklocationRange), ProjectLocationLatitude (aliased from
      ProjectlocLatitude), ProjectLocationLongitude (aliased from
      ProjectlocLongtitude - the database's spelling), MaxLogHoursPerDay
      (aliased from Max_log_hour_day), CountryID (dbo.Signup.CountryID),
      CountryName (dbo.Country.Name), Address (dbo.Signup.Address),
      CurrencyID, CurrencyName and CurrencySymbol (dbo.CountryCurrency_Lookup). The
      aliases are what the EF Core result mapping binds to.

      Country, address and currency are the USER's, from dbo.Signup - not
      the project's or its client's. The currency is the country's: the
      lowest CurrencyID in dbo.CountryCurrency_Lookup for it, the same rule
      spc_AddEditClientSetup uses for a client. A user with no country, or
      a country with no currency, still gets a row, with NULLs.

      ProjectCode / ProjectName and ProjectRoleName come from LEFT JOINs, so
      an assignment whose project or role is NULL, unknown or deleted is
      still returned, with NULL names.

      One row per ASSIGNMENT, not per user: a user on three projects is
      three rows, and a user with no live assignment is not listed.

    Order:
      By user name, then project name, then TimesheetUserProjectSetupID so
      the order is always the same.

    Result:
      An organisation with no timesheet users gets an empty result, not an
      error.

    Depends on TimesheetUserProjectSetupID, which
    ../schema/dbo.Timesheet_Users_Project_setup.sql records as a DRAFT
    column - it must exist before this procedure can run.

    The tables are recorded at:
      ../schema/dbo.Timesheet_Users_Project_setup.sql
      ../schema/dbo.TimesheetProject.sql
      ../schema/dbo.TimesheetUserRole.sql
      ../schema/dbo.Country.sql
      ../schema/dbo.CountryCurrency_Lookup.sql
    NOT RECORDED - dbo.Signup; the test container builds it from the EF
    mapping (SignupConfiguration).
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_GetTimesheetUserListByOrgID]
    @POrgID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        tups.TimesheetUserProjectSetupID    AS TimesheetUserProjectSetupID,
        s.UserID                            AS UserID,
        s.Name                              AS Name,
        s.Email                             AS Email,
        s.OrganizationID                    AS OrganizationID,
        tups.ProjectID                      AS ProjectID,
        tp.Code                             AS ProjectCode,
        tp.Projectname                      AS ProjectName,
        tups.ProjectRoleID                  AS ProjectRoleID,
        tur.Rolename                        AS ProjectRoleName,
        tups.Jobtype                        AS JobType,
        tups.Cost_rate_per_hour             AS CostRate,
        tups.Billable_rate_per_hour         AS BillRate,
        tups.WorklocationRange              AS WorkLocationRange,
        tups.ProjectlocLatitude             AS ProjectLocationLatitude,
        tups.ProjectlocLongtitude           AS ProjectLocationLongitude,
        tups.Max_log_hour_day               AS MaxLogHoursPerDay,
        s.CountryID                         AS CountryID,
        c.Name                              AS CountryName,
        s.Address                           AS Address,
        cc.CurrencyID                       AS CurrencyID,
        cc.CurrencyName                     AS CurrencyName,
        cc.CurrencySymbol                   AS CurrencySymbol
    FROM dbo.Timesheet_Users_Project_setup tups
    INNER JOIN dbo.Signup s
        ON s.UserID = tups.UserID
    LEFT JOIN dbo.TimesheetProject tp
        ON tp.ProjectID = tups.ProjectID
    LEFT JOIN dbo.TimesheetUserRole tur
        ON tur.RoleID = tups.ProjectRoleID
       AND ISNULL(tur.IsDelete, 0) = 0
    LEFT JOIN dbo.Country c
        ON c.ID = s.CountryID
    -- The country's currency: the lowest CurrencyID, should it have several.
    OUTER APPLY
    (
        SELECT TOP (1) currency.CurrencyID, currency.CurrencyName, currency.CurrencySymbol
        FROM dbo.CountryCurrency_Lookup currency
        WHERE currency.CountryID = s.CountryID
        ORDER BY currency.CurrencyID
    ) cc
    WHERE s.OrganizationID = @POrgID
      AND s.IsDelete = 0
      AND ISNULL(tups.IsDelete, 0) = 0
    ORDER BY s.Name, tp.Projectname, tups.TimesheetUserProjectSetupID;
END
GO
