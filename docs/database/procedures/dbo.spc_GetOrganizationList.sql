/*
    Procedure: dbo.spc_GetOrganizationList
    Status:    DRAFT for review - not applied to any database.

    Returns every organisation in dbo.Organization, for
    GET api/v1/MasterAdmin/get-all-organization.

    Parameters:
      None.

    Result columns:
      OrganizationID, OrganizationName, CountryID - the names an EF Core result
      mapping binds to. CreateDate is not returned.

    Order:
      By OrganizationName, then OrganizationID so two organisations with the
      same name always come back in the same order. (The query as supplied had
      no ORDER BY; without one SQL Server may return the rows in any order.)

    Result:
      Every row - dbo.Organization has no soft-delete column. No rows is an
      empty result, not an error.

    The table is not recorded in ../schema/: it is maintained by another
    application. Its mapping is
    src/ETimeSheet.Infrastructure/Data/Configurations/OrganizationConfiguration.cs.
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_GetOrganizationList]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        o.OrganizationID    AS OrganizationID,
        o.OrganizationName  AS OrganizationName,
        o.CountryID         AS CountryID
    FROM dbo.Organization o
    WHERE o.OrganizationName IS NOT NULL
    ORDER BY o.OrganizationName, o.OrganizationID;
END
GO
