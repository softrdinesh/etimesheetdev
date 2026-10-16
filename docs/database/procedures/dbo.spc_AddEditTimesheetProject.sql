/*
    Procedure: dbo.spc_AddEditTimesheetProject
    Status:    DRAFT for review - not applied to any database.
    Revised:   2026-10-13 - @PCode removed: the code is generated on add
               (PR0001, PR0002 ...) and never changed on edit.
    Revised:   2026-10-13 - @PTotalHours is DECIMAL(18,2), following the
               Totalhours column's change from time(7).
    Revised:   2026-10-14 - @PClientID added (required); ClientID and
               ClientName returned.
    Revised:   2026-10-16 - DRAFT: @PSubmissionFrequency added, written on add
               and edit; SubmissionFrequency and SubmissionFrequencyName
               returned.

    Adds a project to an organisation in dbo.TimesheetProject, or updates one,
    and returns the saved row.
    Called by POST api/v1/TimesheetProject/add-edit-timesheet-project.

    Parameters:
      @PProjectID     NULL or 0 adds a new project; any value greater than 0
                      updates that project.
      @POrgID         the organisation the project belongs to. On edit, the
                      project must belong to THIS organisation - an id from
                      another organisation is treated as not found. An edit
                      never moves a project to another organisation.
      @PProjectName   the project's name.
      @PTotalBudget   the budget, decimal(18,2).
      @PTotalHours    the hours budget, decimal(18,2) - a number of hours.
      @PStartDate     the start date.
      @PEndDate       the end date.
      @PCurrencyCode  the currency.
      @PStatus        a dbo.TimesheetProjectStatus.StatusID.
      @PClientID      the client the project is for -
                      dbo.TimesheetClientSetup.ClientID. Required: the
                      column is NOT NULL. Written on add and on edit, so an
                      edit can move a project to another client.
      @PSubmissionFrequency  how often the project's timesheets are
                      submitted: 1 weekly, 2 every 15 days, 3 monthly, or
                      NULL. The table's CHECK constraint rejects anything
                      else.

    Add (@PProjectID NULL or 0):
      The project gets the next code in the sequence PR0001, PR0002 ...
      PR9999, PR10000 ... - the number keeps counting, zero-padded to at
      least four digits, exactly as spc_AddEditClientSetup numbers clients
      CL0001 .... "Next" follows the highest existing code of the form
      PR + digits across EVERY row, of every organisation, so a code is
      unique across the whole table.

      The latest code is read WITH (UPDLOCK, HOLDLOCK) inside the insert's
      transaction, so two adds at the same moment cannot both pick the same
      code: the second waits for the first to commit, then reads its code.
      A code longer than the column's 20 characters raises error 50001
      rather than being truncated into a duplicate.

      ProjectID is IDENTITY(1,1), so the database assigns it; the new id is
      read back with SCOPE_IDENTITY().

    Edit (@PProjectID > 0):
      Overwrites every column except ProjectID, Code and OrganizationID, only
      if the project belongs to @POrgID. Code never changes. A NULL parameter
      writes NULL - the payload is the whole project, not a patch.

    Result:
      The saved project - the same columns as dbo.spc_GetTimesheetProject. NO
      ROW when an edit names a project that does not exist or belongs to
      another organisation; the API answers 404 for that.

    Validation lives in the API (TimesheetProjectService): a positive @POrgID,
    a @PProjectName of at most 100 characters, a non-negative @PTotalBudget and
    @PTotalHours, @PEndDate not before
    @PStartDate, a positive @PStatus when sent, a @PSubmissionFrequency of
    1, 2 or 3 when sent, and a @PClientID that names a live (not deleted)
    client. The
    procedure does not repeat it. Neither checks that @PStatus names a
    dbo.TimesheetProjectStatus row - no foreign key was reported.

    The tables are recorded at:
      ../schema/dbo.TimesheetProject.sql
      ../schema/dbo.TimesheetProjectStatus.sql
      ../schema/dbo.Timesheetclientsetup.sql
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_AddEditTimesheetProject]
    @PProjectID     INT             = NULL,
    @POrgID         INT,
    @PProjectName   NVARCHAR(100)   = NULL,
    @PTotalBudget   DECIMAL(18,2)   = NULL,
    @PTotalHours    DECIMAL(18,2)   = NULL,
    @PStartDate     DATETIME        = NULL,
    @PEndDate       DATETIME        = NULL,
    @PCurrencyCode  INT             = NULL,
    @PStatus        INT             = NULL,
    @PClientID      INT,
    @PSubmissionFrequency INT       = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SET XACT_ABORT ON;

    DECLARE @ProjectID INT;

    IF ISNULL(@PProjectID, 0) = 0
    BEGIN
        BEGIN TRANSACTION;

        -- =========================================================
        -- The highest generated code, held locked until COMMIT
        -- =========================================================
        -- Longest first, then greatest: PR10000 outranks PR9999, which a
        -- plain text comparison would get backwards. Only PR + digits counts.
        DECLARE @LatestCode NVARCHAR(20) =
        (
            SELECT TOP (1) tp.Code
            FROM dbo.TimesheetProject tp WITH (UPDLOCK, HOLDLOCK)
            WHERE tp.Code LIKE 'PR[0-9]%'
              AND SUBSTRING(tp.Code, 3, 18) NOT LIKE '%[^0-9]%'
            ORDER BY LEN(tp.Code) DESC, tp.Code DESC
        );

        DECLARE @NextNumber BIGINT =
            ISNULL(TRY_CAST(SUBSTRING(@LatestCode, 3, 18) AS BIGINT), 0) + 1;

        DECLARE @NextDigits VARCHAR(20) = CAST(@NextNumber AS VARCHAR(20));

        DECLARE @NextCode VARCHAR(25) = 'PR' +
            CASE WHEN LEN(@NextDigits) < 4
                 THEN RIGHT('0000' + @NextDigits, 4)
                 ELSE @NextDigits
            END;

        IF LEN(@NextCode) > 20
            THROW 50001, 'The project code sequence has run out of room: Code holds at most 20 characters.', 1;

        INSERT INTO dbo.TimesheetProject
            (Code, Projectname, TotalBudget, Totalhours, StartDate, EndDate, CurrencyCode, OrganizationID, Status, ClientID, SubmissionFrequency)
        VALUES
            (@NextCode, @PProjectName, @PTotalBudget, @PTotalHours, @PStartDate, @PEndDate, @PCurrencyCode, @POrgID, @PStatus, @PClientID, @PSubmissionFrequency);

        SET @ProjectID = CAST(SCOPE_IDENTITY() AS INT);

        COMMIT TRANSACTION;
    END
    ELSE
    BEGIN
        UPDATE dbo.TimesheetProject
        SET Projectname  = @PProjectName,
            TotalBudget  = @PTotalBudget,
            Totalhours   = @PTotalHours,
            StartDate    = @PStartDate,
            EndDate      = @PEndDate,
            CurrencyCode = @PCurrencyCode,
            Status       = @PStatus,
            ClientID     = @PClientID,
            SubmissionFrequency = @PSubmissionFrequency
        WHERE ProjectID = @PProjectID
          AND OrganizationID = @POrgID;

        SET @ProjectID = @PProjectID;
    END

    -- =================================================================
    -- The saved row - same columns as spc_GetTimesheetProject
    -- =================================================================
    SELECT
        tp.ProjectID        AS ProjectID,
        tp.Code             AS Code,
        tp.Projectname      AS ProjectName,
        tp.TotalBudget      AS TotalBudget,
        tp.Totalhours       AS TotalHours,
        tp.StartDate        AS StartDate,
        tp.EndDate          AS EndDate,
        tp.CurrencyCode     AS CurrencyCode,
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
    WHERE tp.ProjectID = @ProjectID
      AND tp.OrganizationID = @POrgID;
END
GO
