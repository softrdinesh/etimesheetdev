/* =============================================================================
   Project timesheet submission reminder - lookup rows
   -----------------------------------------------------------------------------
   Run by hand, once, by the database owner. Never run by the application or
   the tests (docs/database/data/ is not replayed).

   Inserts the two reference rows the ProjectTimesheetReminderEmailQueue job
   needs. Until both exist the job does nothing: with no SchedulerConfiguration
   row it is logged at startup as "has no row with that id" and never
   scheduled.

     dbo.EmailType               3 = 'Project Timesheet Submission Reminder'
                                 (Constants.EmailType.Id.ProjectSheetSubmissionReminder)
     dbo.SchedulerConfiguration  4 = 'ProjectTimesheetSubmissionReminderEmailQueue'
                                 (SchedulerJobs.ProjectTimesheetReminderEmailQueue),
                                 Repeated every 1 minute from 2026-10-08 00:09:02
                                 UTC, like the other three jobs.

   Ids     : explicit, under IDENTITY_INSERT - the code looks both rows up by
             id, so they must be 3 and 4 in every database.
   Idempotent : a row is skipped if its id or its name is already in the
             table, so re-running inserts nothing twice.
   Afterwards : restart the API - the scheduler reads its configuration once,
             at startup.

   Before running, check that ids 3 and 4 are free; if either is taken by a
   different row, stop and tell the developer - the constants in code have to
   change with it:
       SELECT * FROM dbo.EmailType              WHERE EmailTypeID = 3;
       SELECT * FROM dbo.SchedulerConfiguration WHERE SchedulerConfigurationID = 4;
   ============================================================================= */

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

-- dbo.EmailType ---------------------------------------------------------------

SET IDENTITY_INSERT dbo.EmailType ON;

INSERT INTO dbo.EmailType (EmailTypeID, EmailTypeName)
SELECT v.EmailTypeID, v.EmailTypeName
FROM (VALUES (CAST(3 AS tinyint), 'Project Timesheet Submission Reminder')) AS v (EmailTypeID, EmailTypeName)
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.EmailType AS t
    WHERE t.EmailTypeID = v.EmailTypeID
       OR t.EmailTypeName = v.EmailTypeName
);

SET IDENTITY_INSERT dbo.EmailType OFF;

-- dbo.SchedulerConfiguration --------------------------------------------------

SET IDENTITY_INSERT dbo.SchedulerConfiguration ON;

INSERT INTO dbo.SchedulerConfiguration
    (SchedulerConfigurationID, SchedulerName, IsEnabled, ScheduleTypeID, ScheduleDateTime,
     IsRepeatEnabled, RepeatInterval, RepeatIntervalType, CreatedDate)
SELECT
    v.SchedulerConfigurationID, v.SchedulerName, v.IsEnabled, v.ScheduleTypeID, v.ScheduleDateTime,
    v.IsRepeatEnabled, v.RepeatInterval, v.RepeatIntervalType, GETDATE()
FROM
(
    VALUES
        (4, 'ProjectTimesheetSubmissionReminderEmailQueue', CAST(1 AS bit), CAST(2 AS tinyint),
         CAST('2026-10-08 00:09:02' AS datetime2(0)), CAST(1 AS bit), 1, 'M')
) AS v (SchedulerConfigurationID, SchedulerName, IsEnabled, ScheduleTypeID, ScheduleDateTime,
        IsRepeatEnabled, RepeatInterval, RepeatIntervalType)
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.SchedulerConfiguration AS s
    WHERE s.SchedulerConfigurationID = v.SchedulerConfigurationID
       OR s.SchedulerName = v.SchedulerName
);

SET IDENTITY_INSERT dbo.SchedulerConfiguration OFF;

COMMIT TRANSACTION;

SELECT EmailTypeID, EmailTypeName FROM dbo.EmailType ORDER BY EmailTypeID;
SELECT SchedulerConfigurationID, SchedulerName, IsEnabled, ScheduleTypeID, RepeatInterval, RepeatIntervalType
FROM dbo.SchedulerConfiguration
ORDER BY SchedulerConfigurationID;
