/* =============================================================================
   dbo.Timesheet_Users_Project_setup -- add IsTimeLogDisabled
   -----------------------------------------------------------------------------
   Run by     : the database owner, by hand. This is the runnable script.
                The RECORD of the table lives at
                ../schema/dbo.Timesheet_Users_Project_setup.sql and begins with
                DROP TABLE -- do not run that one.
   Purpose    : adds the per-assignment switch that turns time logging off.
   Idempotent : safe to re-run. The column and its default are added only when
                they are missing; a second run prints and changes nothing.
   Destructive: no. It adds one column to an existing table. No row is
                deleted, no column is dropped, and no other table is touched.

   WHAT THE VALUE MEANS -- read this before using the column:

       0  (the default)  the assignment works normally
       NULL              the same as 0 - nothing is turned off
       1                 an administrator has turned time logging OFF

   With 1, the API refuses to log, change or remove time on the assignment,
   and refuses to submit its timesheets. Only 1 disables: NULL is read exactly
   as 0, as it already is for IsDelete on this table, so a row written by hand
   without the column is left working.

   WHY int NULL with a default of 0: it matches IsDelete on this table, which
   is also a nullable int used as a flag. WITH VALUES below means the default
   is written to every EXISTING row as well, not only to rows inserted later -
   without it they would all be left NULL. Either way none of them is blocked,
   because only 1 blocks.

   AFTER RUNNING THIS, apply the two procedure revisions that read and write
   the column, or the endpoints that use it will still not see it:
       ../procedures/dbo.spc_Signup_timesheet_user.sql
       ../procedures/dbo.spc_GetTimesheetUserListByOrgID.sql
       ../procedures/dbo.spc_GetTimesheetUserDetailsByUserID.sql
   ============================================================================= */

SET NOCOUNT ON;
GO

IF COL_LENGTH(N'dbo.Timesheet_Users_Project_setup', N'IsTimeLogDisabled') IS NULL
BEGIN
    ALTER TABLE dbo.Timesheet_Users_Project_setup
        ADD IsTimeLogDisabled int NULL
            CONSTRAINT DF_Timesheet_Users_Project_setup_IsTimeLogDisabled DEFAULT (0)
            WITH VALUES;

    PRINT 'dbo.Timesheet_Users_Project_setup.IsTimeLogDisabled added, defaulting to 0 (time logging allowed).';
END
ELSE
BEGIN
    PRINT 'dbo.Timesheet_Users_Project_setup.IsTimeLogDisabled already exists; left as it is.';
END
GO

/* The default constraint, should the column have been added without one by an
   earlier hand-run. Adding it does not change any existing row. */
IF COL_LENGTH(N'dbo.Timesheet_Users_Project_setup', N'IsTimeLogDisabled') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.default_constraints dc
       INNER JOIN sys.columns c
           ON c.object_id = dc.parent_object_id
          AND c.column_id = dc.parent_column_id
       WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Timesheet_Users_Project_setup')
         AND c.name = N'IsTimeLogDisabled')
BEGIN
    ALTER TABLE dbo.Timesheet_Users_Project_setup
        ADD CONSTRAINT DF_Timesheet_Users_Project_setup_IsTimeLogDisabled
            DEFAULT (0) FOR IsTimeLogDisabled;

    PRINT 'Default constraint added for IsTimeLogDisabled.';
END
GO

/* What the table now holds. 0 or NULL = works normally, 1 = time logging off. */
SELECT
    IsTimeLogDisabled,
    COUNT(*) AS Assignments
FROM dbo.Timesheet_Users_Project_setup
GROUP BY IsTimeLogDisabled
ORDER BY IsTimeLogDisabled;
GO
