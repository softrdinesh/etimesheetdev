/*
    Table: dbo.SchedulerConfiguration
    Recorded: 2026-10-08 (DDL supplied by the database owner, who created the
              table by hand)
    Revised:  2026-10-08 - header only: mapped and read by the Scheduler module.
              Owner's rows at that date: 1 = SendEmail, 2 =
              TimeLogReminderEmailQueue - both Repeated from
              2026-10-08 00:09:02, every 32 and 15 minutes respectively.

    One row per named background job, describing when it runs: once, on a
    repeat interval, daily, weekly or monthly, and whether it is switched on.

    Mapped by:
      src/ETimeSheet.Application/Models/Entities/SchedulerConfiguration.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/SchedulerConfigurationConfiguration.cs

    Read by (never written - rows are maintained by hand):
      src/ETimeSheet.Infrastructure/Repositories/SchedulerRepository.cs
      (once, at startup, via ScheduledJobsHostedService ->
      SchedulerService.StartScheduledJobsAsync)

    How the API reads a row (SchedulerService.ToSchedule):
      - A job is looked up by SchedulerConfigurationID, the id held for it in
        src/ETimeSheet.Shared/Constants/SchedulerJobs.cs:
          1 = SendEmail, 2 = TimeLogReminderEmailQueue,
          3 = TimesheetReminderEmailQueue.
        SchedulerName is for people; the code does not read it to find a job.
      - A row with IsEnabled = 0 is not scheduled. Changes take effect on the
        next restart of the API.
      - ScheduleDateTime is UTC. (CreatedDate is not - it is GETDATE(),
        the server's local time.)
      - Once      needs ScheduleDateTime; a time already passed is not run.
      - Repeated  needs IsRepeatEnabled = 1, RepeatInterval, RepeatIntervalType;
                  ScheduleDateTime optional (null = first run immediately at
                  startup). With a start time, runs always fall on
                  start + n * interval, whenever the API is restarted.
      - Daily / Weekly / Monthly need ScheduleDateTime; the repeat columns are
                  ignored. Monthly on the 31st runs on each month's last day.
      - A row missing what its type needs is logged, and its job does not run.

    Column meanings that are not obvious from the type:
      - ScheduleTypeID      1 = Once, 2 = Repeated, 3 = Daily, 4 = Weekly,
                            5 = Monthly (stated in the owner's DDL comments).
      - ScheduleDateTime    the first / initial execution time.
      - RepeatInterval      how many RepeatIntervalType units between runs;
                            must be > 0 when present.
      - RepeatIntervalType  'H' = hours, 'M' = minutes, 'S' = seconds.

    Column quirks worth knowing:
      - SchedulerName is varchar(150) and UNIQUE - the natural key a job is
        looked up by.
      - ScheduleTypeID is tinyint, not int: maps to byte in C#.
      - ScheduleDateTime and CreatedDate are datetime2(0) - whole seconds, no
        fractions - unlike the plain datetime columns on dbo.TimeLog.
      - CreatedDate defaults to GETDATE(), which is the SQL Server's LOCAL time,
        not UTC.
      - IsEnabled defaults to 1, IsRepeatEnabled to 0.
      - RepeatIntervalType is char(1), checked to NULL or one of 'H', 'M', 'S'.
        The check is case-sensitive only if the column's collation is; under the
        database default (case-insensitive) 'h' also passes.

    STILL UNCONFIRMED:
      - The primary key and default constraints were left unnamed in the DDL,
        so SQL Server generated their names; they are recorded unnamed.
      - Collation of SchedulerName and RepeatIntervalType - recorded as the
        database default.
      - How ScheduleTypeID 2 (Repeated) relates to IsRepeatEnabled - whether
        one implies the other, or a Daily/Weekly/Monthly job may also repeat
        within its day.
      - For Weekly and Monthly, which day the job runs on: no column names one,
        so presumably the day of ScheduleDateTime.
*/

IF OBJECT_ID(N'dbo.SchedulerConfiguration', N'U') IS NOT NULL
    DROP TABLE dbo.SchedulerConfiguration;
GO

CREATE TABLE dbo.SchedulerConfiguration
(
    SchedulerConfigurationID  int            IDENTITY(1,1) PRIMARY KEY,
    SchedulerName             varchar(150)   NOT NULL,
    IsEnabled                 bit            NOT NULL DEFAULT 1,
    ScheduleTypeID            tinyint        NOT NULL,
    ScheduleDateTime          datetime2(0)   NULL,
    IsRepeatEnabled           bit            NOT NULL DEFAULT 0,
    RepeatInterval            int            NULL,
    RepeatIntervalType        char(1)        NULL,
    CreatedDate               datetime2(0)   NOT NULL DEFAULT GETDATE(),

    CONSTRAINT UQ_SchedulerConfiguration_SchedulerName
        UNIQUE (SchedulerName),

    CONSTRAINT CK_SchedulerConfiguration_RepeatIntervalType
        CHECK (RepeatIntervalType IS NULL OR RepeatIntervalType IN ('H', 'M', 'S')),

    CONSTRAINT CK_SchedulerConfiguration_RepeatInterval
        CHECK (RepeatInterval IS NULL OR RepeatInterval > 0)
);
GO

-- The three schedulers. The ids are fixed - the code looks each job up by its
-- SchedulerConfigurationID (src/ETimeSheet.Shared/Constants/SchedulerJobs.cs) -
-- so they are inserted explicitly rather than left to the IDENTITY.
-- All three repeat every minute from 2026-10-08 00:09:02 UTC.
SET IDENTITY_INSERT dbo.SchedulerConfiguration ON;

INSERT INTO dbo.SchedulerConfiguration
    (SchedulerConfigurationID, SchedulerName, IsEnabled, ScheduleTypeID, ScheduleDateTime,
     IsRepeatEnabled, RepeatInterval, RepeatIntervalType, CreatedDate)
VALUES
    (1, 'SendEmail',                             1, 2, '2026-10-08 00:09:02', 1, 1, 'M', '2026-10-08 00:09:02'),
    (2, 'TimeLogReminderEmailQueue',             1, 2, '2026-10-08 00:09:02', 1, 1, 'M', '2026-10-08 00:09:02'),
    (3, 'TimeSheetSubmissionReminderEmailQueue', 1, 2, '2026-10-08 00:09:02', 1, 1, 'M', '2026-10-08 00:09:02');

SET IDENTITY_INSERT dbo.SchedulerConfiguration OFF;
GO
