/*
    Procedure: dbo.spc_AddEditTimesheetHolidaySetup
    Status:    DRAFT for review - not applied to any database.
    Recorded:  2026-10-17 - DRAFT: new procedure.
    Revised:   2026-10-18 - DRAFT: @PDescription added, written to the new
               Description column and returned.

    Adds a holiday to dbo.TimesheetHolidaysetup, or edits one, and returns the
    saved row. Called by
    POST api/v1/TimesheetProject/add-edit-timesheet-holiday-setup.

    Parameters:
      @PTimesheetHolidaySetupID  NULL or 0 adds a new holiday; any other value
                                 edits that holiday.
      @PHolidayDate              the holiday's date.
      @PDescription              optional, the holiday's name; NULL clears it
                                 on edit.
      @POrgID                    the organisation the holiday belongs to. On
                                 edit, the holiday must belong to THIS
                                 organisation - an id from another
                                 organisation is treated as not found.
      @PCountryID                optional; NULL clears it on edit.
      @PClientID                 optional; NULL clears it on edit.
      @PProjectID                optional; NULL clears it on edit.
      @PUserID                   written to CreatedBy on add. The table has no
                                 "updated by" column, so an edit records
                                 nobody.
      @PDate                     written to CreateDate on add - the API's
                                 clock (UTC), passed in so the procedure and
                                 the API agree on "now".

    Add (@PTimesheetHolidaySetupID NULL or 0):
      Inserts a live row (IsDelete = 0). TimesheetHolidaysetupID is
      IDENTITY(1,1), so the database assigns it; the new id is read back with
      SCOPE_IDENTITY().

    Edit (@PTimesheetHolidaySetupID > 0):
      Updates HolidayDate, Description, CountryID, ClientID and ProjectID
      only if the
      holiday belongs to @POrgID and is live (IsDelete 0 or NULL). A deleted
      holiday is not edited back to life. CreateDate / CreatedBy are left as
      they were.

    Result:
      The saved holiday - the same columns as
      dbo.spc_GetTimesheetHolidaySetup. NO ROW when an edit names a holiday
      that does not exist, is deleted, or belongs to another organisation; the
      API answers 404 for that.

    Validation lives in the API (TimesheetProjectService): a positive @POrgID
    and @PUserID, a @PHolidayDate, a @PDescription of at most 100
    characters, and a country, client and project that
    exist - the client and project in @POrgID. The procedure does not repeat
    it.

    The tables are recorded at:
      ../schema/dbo.TimesheetHolidaysetup.sql
      ../schema/dbo.Country.sql
      ../schema/dbo.Timesheetclientsetup.sql
      ../schema/dbo.TimesheetProject.sql
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_AddEditTimesheetHolidaySetup]
    @PTimesheetHolidaySetupID  INT       = NULL,
    @PHolidayDate              DATE,
    @PDescription              NVARCHAR(100) = NULL,
    @POrgID                    INT,
    @PCountryID                INT       = NULL,
    @PClientID                 INT       = NULL,
    @PProjectID                INT       = NULL,
    @PUserID                   INT,
    @PDate                     DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    SET XACT_ABORT ON;

    DECLARE @TimesheetHolidaySetupID INT;

    IF ISNULL(@PTimesheetHolidaySetupID, 0) = 0
    BEGIN
        INSERT INTO dbo.TimesheetHolidaysetup
            (HolidayDate, Description, OrganizationID, CountryID, ClientID, ProjectID, CreateDate, CreatedBy, IsDelete)
        VALUES
            (@PHolidayDate, @PDescription, @POrgID, @PCountryID, @PClientID, @PProjectID, @PDate, @PUserID, 0);

        SET @TimesheetHolidaySetupID = CAST(SCOPE_IDENTITY() AS INT);
    END
    ELSE
    BEGIN
        UPDATE dbo.TimesheetHolidaysetup
        SET HolidayDate = @PHolidayDate,
            Description = @PDescription,
            CountryID   = @PCountryID,
            ClientID    = @PClientID,
            ProjectID   = @PProjectID
        WHERE TimesheetHolidaysetupID = @PTimesheetHolidaySetupID
          AND OrganizationID = @POrgID
          AND ISNULL(IsDelete, 0) = 0;

        SET @TimesheetHolidaySetupID = @PTimesheetHolidaySetupID;
    END

    -- =================================================================
    -- The saved row - same columns as spc_GetTimesheetHolidaySetup
    -- =================================================================
    SELECT
        ths.TimesheetHolidaysetupID AS TimesheetHolidaySetupID,
        ths.HolidayDate             AS HolidayDate,
        ths.Description             AS Description,
        ths.OrganizationID          AS OrganizationID,
        ths.CountryID               AS CountryID,
        c.Name                      AS CountryName,
        ths.ClientID                AS ClientID,
        tcs.Clientname              AS ClientName,
        ths.ProjectID               AS ProjectID,
        tp.Code                     AS ProjectCode,
        tp.Projectname              AS ProjectName,
        ths.CreateDate              AS CreateDate,
        ths.CreatedBy               AS CreatedBy
    FROM dbo.TimesheetHolidaysetup ths
    LEFT JOIN dbo.Country c ON c.ID = ths.CountryID
    LEFT JOIN dbo.TimesheetClientSetup tcs ON tcs.ClientID = ths.ClientID
    LEFT JOIN dbo.TimesheetProject tp ON tp.ProjectID = ths.ProjectID
    WHERE ths.TimesheetHolidaysetupID = @TimesheetHolidaySetupID
      AND ths.OrganizationID = @POrgID
      AND ISNULL(ths.IsDelete, 0) = 0;
END
GO
