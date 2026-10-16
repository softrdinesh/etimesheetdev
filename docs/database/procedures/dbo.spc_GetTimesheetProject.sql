/*
    Procedure: dbo.spc_GetTimesheetProject
    Status:    DRAFT for review - not applied to any database.
    Revised:   2026-10-14 - ClientID and ClientName (from
               dbo.TimesheetClientSetup) added to the result.
    Revised:   2026-10-16 - DRAFT: SubmissionFrequency and
               SubmissionFrequencyName added to the result.
    Revised:   2026-10-17 - DRAFT: CurrencySymbol (from dbo.CountryCurrency)
               added to the result, after CurrencyCode.

    Returns the projects of one organisation from dbo.TimesheetProject, for
    GET api/v1/TimesheetProject/get-timesheet-project-by-orgID/{orgID}.

    Parameters:
      @POrgID  the organisation (dbo.TimesheetProject.OrganizationID).

    Projects:
      Every row of the organisation. dbo.TimesheetProject has no soft-delete
      column - a removed project is physically deleted, so it is simply gone.

    Result columns:
      ProjectID, Code, ProjectName (aliased from Projectname), TotalBudget,
      TotalHours (aliased from Totalhours), StartDate, EndDate, CurrencyCode,
      CurrencySymbol from dbo.CountryCurrency, Status, StatusName from dbo.TimesheetProjectStatus, ClientID,
      ClientName from dbo.TimesheetClientSetup, SubmissionFrequency
      (1 weekly, 2 every 15 days, 3 monthly; NULL when not set), and
      SubmissionFrequencyName - its text, 'Weekly', 'Every 15 Days' or
      'Monthly', from a CASE; NULL when SubmissionFrequency is NULL. The
      aliases are
      what an EF Core result mapping binds to. OrganizationID is not returned -
      the caller already knows it.

      StatusName comes from a LEFT JOIN, so a project whose Status is NULL or
      names no row in dbo.TimesheetProjectStatus is still returned, with a NULL
      StatusName.

      ClientName comes from a LEFT JOIN too, and is NOT filtered on
      IsDeleted: a project keeps showing the name of a client that has since
      been removed. Only a ClientID that names no row gives a NULL ClientName.

      CurrencySymbol comes from a LEFT JOIN on CountryCurrency.CurrencyID =
      CurrencyCode, so a project whose CurrencyCode is NULL or names no row
      is still returned, with a NULL CurrencySymbol. Symbols are not unique
      ("$" is shared by many currencies).

    Order:
      By ProjectName, then ProjectID so two projects with the same name always
      come back in the same order.

    Result:
      An organisation with no projects gets an empty result, not an error.

    The tables are recorded at:
      ../schema/dbo.TimesheetProject.sql
      ../schema/dbo.TimesheetProjectStatus.sql
      ../schema/dbo.Timesheetclientsetup.sql
      ../schema/dbo.CountryCurrency.sql
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_GetTimesheetProject]
    @POrgID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        tp.ProjectID        AS ProjectID,
        tp.Code             AS Code,
        tp.Projectname      AS ProjectName,
        tp.TotalBudget      AS TotalBudget,
        tp.Totalhours       AS TotalHours,
        tp.StartDate        AS StartDate,
        tp.EndDate          AS EndDate,
        tp.CurrencyCode     AS CurrencyCode,
        cc.CurrencySymbol   AS CurrencySymbol,
        tp.Status           AS Status,
        tps.Status          AS StatusName,
        tp.ClientID         AS ClientID,
        tcs.Clientname      AS ClientName,
        tp.SubmissionFrequency AS SubmissionFrequency,
        CASE tp.SubmissionFrequency
            WHEN 1 THEN 'Weekly'
            WHEN 2 THEN 'Every 15 Days'
            WHEN 3 THEN 'Monthly'
        END                 AS SubmissionFrequencyName
    FROM dbo.TimesheetProject tp
    LEFT JOIN dbo.TimesheetProjectStatus tps
        ON tps.StatusID = tp.Status
    LEFT JOIN dbo.TimesheetClientSetup tcs
        ON tcs.ClientID = tp.ClientID
    LEFT JOIN dbo.CountryCurrency cc
        ON cc.CurrencyID = tp.CurrencyCode
    WHERE tp.OrganizationID = @POrgID
    ORDER BY tp.Projectname, tp.ProjectID;
END
GO
