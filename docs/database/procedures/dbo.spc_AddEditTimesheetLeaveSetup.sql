/*
    Procedure: dbo.spc_AddEditTimesheetLeaveSetup
    Status:    DRAFT for review - not applied to any database.
    Recorded:  2026-10-25 - DRAFT: new procedure.
    Revised:   2026-10-25 - DRAFT: result reduced to the columns of the
               owner's revised dbo.spc_GetTimesheetLeaveSetup - the
               audit columns are no longer returned (still written).

    Adds a leave type to an organisation in dbo.Timesheet_Leavetype_Setup, or
    edits one, and returns the saved row. Called by
    POST api/v1/TimesheetProject/add-edit-timesheet-leave-setup.

    Parameters:
      @PLeaveTypeID           NULL or 0 adds a new leave type; any other value
                              edits that leave type.
      @POrgID                 the organisation the leave type belongs to. On
                              edit, the leave type must belong to THIS
                              organisation - an id from another organisation
                              is treated as not found.
      @PLeaveType             the leave type's name - "Sick Leave".
      @PNoOfLeaveAllocated    the days allocated, at most 99.99.
      @PLoginUserID           written to CreatedBy on add, UpdatedBy on edit.
      @PDate                  written to CreateDate on add, UpdatedDate on
                              edit - the API's clock (UTC), passed in so the
                              procedure and the API agree on "now".

    Add (@PLeaveTypeID NULL or 0):
      Inserts a live row (IsDelete = 0). LeaveTypeID is IDENTITY(1,1), so the
      database assigns it; the new id is read back with SCOPE_IDENTITY().

    Edit (@PLeaveTypeID > 0):
      Updates LeaveType and NoofLeaveAllocated, and stamps UpdatedDate /
      UpdatedBy, only if the leave type belongs to @POrgID and is live
      (ISNULL(IsDelete, 0) = 0). A deleted leave type is not edited back to
      life. CreateDate / CreatedBy are left as they were.

    Result:
      The saved leave type - the same columns as
      dbo.spc_GetTimesheetLeaveSetup. NO ROW when an edit names a leave type
      that does not exist, is deleted, or belongs to another organisation;
      the API answers 404 for that.

    Validation lives in the API (TimesheetProjectService): a positive
    @POrgID and @PLoginUserID, a non-blank @PLeaveType of at most 50
    characters, and a @PNoOfLeaveAllocated from 0 to 99.99 with at most two
    decimal places. The procedure does not repeat it.

    The table is recorded at:
      ../schema/dbo.Timesheet_Leavetype_Setup.sql
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_AddEditTimesheetLeaveSetup]
    @PLeaveTypeID           INT           = NULL,
    @POrgID                 INT,
    @PLeaveType             NVARCHAR(50),
    @PNoOfLeaveAllocated    DECIMAL(4,2),
    @PLoginUserID           INT,
    @PDate                  DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    SET XACT_ABORT ON;

    DECLARE @LeaveTypeID INT = ISNULL(@PLeaveTypeID, 0);

    IF @LeaveTypeID = 0
    BEGIN
        INSERT INTO dbo.Timesheet_Leavetype_Setup
            (LeaveType, NoofLeaveAllocated, OrgID, CreateDate, CreatedBy, IsDelete)
        VALUES
            (@PLeaveType, @PNoOfLeaveAllocated, @POrgID, @PDate, @PLoginUserID, 0);

        SET @LeaveTypeID = CAST(SCOPE_IDENTITY() AS INT);
    END
    ELSE
    BEGIN
        UPDATE dbo.Timesheet_Leavetype_Setup
        SET LeaveType          = @PLeaveType,
            NoofLeaveAllocated = @PNoOfLeaveAllocated,
            UpdatedDate        = @PDate,
            UpdatedBy          = @PLoginUserID
        WHERE LeaveTypeID = @LeaveTypeID
          AND OrgID = @POrgID
          AND ISNULL(IsDelete, 0) = 0;
    END

    -- =================================================================
    -- The saved row - same columns as spc_GetTimesheetLeaveSetup
    -- =================================================================
    SELECT
        tls.LeaveTypeID             AS LeaveTypeID,
        tls.OrgID                   AS OrganizationID,
        tls.LeaveType               AS LeaveType,
        tls.NoofLeaveAllocated      AS NoOfLeaveAllocated
    FROM dbo.Timesheet_Leavetype_Setup tls
    WHERE tls.LeaveTypeID = @LeaveTypeID
      AND tls.OrgID = @POrgID
      AND ISNULL(tls.IsDelete, 0) = 0;
END
GO
