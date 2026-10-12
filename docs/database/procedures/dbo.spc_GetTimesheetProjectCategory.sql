/*
    Procedure: dbo.spc_GetTimesheetProjectCategory
    Written:   2026-10-12 - to be applied by the database owner, by hand.

    Returns the live project categories of one organisation from
    dbo.TimesheetProjectCategory, for
    GET api/v1/Admin/get-timesheet-project-category-by-orgID/{orgID}.

    Parameters:
      @POrgID  the organisation (dbo.TimesheetProjectCategory.OrganizationID).

    Categories:
      Only live rows - IsDelete 0 OR NULL. The column is a nullable bit, and
      only 1 means deleted, so the test is ISNULL(IsDelete, 0) = 0; a plain
      "IsDelete = 0" would silently drop every category whose flag was never
      set.

    Result columns:
      CategoryID (aliased from ID), CategoryName (aliased from Categoryname).
      The aliases are what an EF Core result mapping binds to. OrganizationID
      is not returned - the caller already knows it.

    Order:
      By CategoryName, then CategoryID so two categories with the same name
      always come back in the same order.

    Result:
      An organisation with no categories gets an empty result, not an error.

    The table is recorded at:
      ../schema/dbo.TimesheetProjectCategory.sql
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_GetTimesheetProjectCategory]
    @POrgID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        tpc.ID              AS CategoryID,
        tpc.Categoryname    AS CategoryName
    FROM dbo.TimesheetProjectCategory tpc
    WHERE tpc.OrganizationID = @POrgID
      AND ISNULL(tpc.IsDelete, 0) = 0
    ORDER BY tpc.Categoryname, tpc.ID;
END
GO
