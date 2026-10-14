/* =============================================================================
   dbo.TimesheetProjectStatus — populate the project status lookup
   -----------------------------------------------------------------------------
   Run by hand, once, AFTER dbo.TimesheetProjectStatus has been created
   (../schema/dbo.TimesheetProjectStatus.sql). Never run by the application or
   the tests.

   Rows       : 3 - Open, Closed, Pending.
   StatusID   : explicit, 1 - 3, so an id means the same status in every
                database. Inserted under IDENTITY_INSERT.
   Idempotent : a row is skipped if its StatusID or its Status is already in
                the table, so re-running inserts nothing twice.
   ============================================================================= */

SET NOCOUNT ON;

BEGIN TRANSACTION;

SET IDENTITY_INSERT dbo.TimesheetProjectStatus ON;

INSERT INTO dbo.TimesheetProjectStatus
    (StatusID, Status)
SELECT
    v.StatusID, v.Status
FROM
(
    VALUES
        (1, N'Open'),
        (2, N'Closed'),
        (3, N'Pending')
) AS v (StatusID, Status)
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.TimesheetProjectStatus AS s
    WHERE s.StatusID = v.StatusID
       OR s.Status   = v.Status
);

SET IDENTITY_INSERT dbo.TimesheetProjectStatus OFF;

COMMIT TRANSACTION;
