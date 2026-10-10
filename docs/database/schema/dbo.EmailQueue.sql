/*
    Table: dbo.EmailQueue
    Recorded: 2026-10-08 (DDL supplied by the database owner, who created the
              table by hand)
    Revised:  2026-10-08 - ProjectID removed by the database owner.

    One row per email to be sent: who it goes to, what kind it is, and how far
    sending it has got - pending, in progress, sent, or failed - with the
    attempt count and the last error.

    Mapped by:
      src/ETimeSheet.Application/Models/Entities/EmailQueue.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/EmailQueueConfiguration.cs

    Written by:
      src/ETimeSheet.Infrastructure/Repositories/EmailQueueRepository.cs
      - insert, by the TimeLogReminderEmailQueue background job: one Pending
        Time Log Reminder (EmailTypeID 1) per user due one, at most one per
        user per day on the user's own clock - decided in
        SchedulerService.RunTimeLogReminderEmailQueueAsync.
      - insert, by the TimesheetReminderEmailQueue background job: one Pending
        Sheet Submission Reminder (EmailTypeID 2) per user due one - the last
        day of their week, week not yet submitted - at most one per user per
        day - decided in SchedulerService.RunTimesheetReminderEmailQueueAsync.
      - update, by POST /api/v1/Admin/delete-timesheet-setup: the user's
        Pending and Error rows become EmailStatusID 2 (Sent) with
        ErrorMessage 'Setup deleted' and SentDate left NULL, so the sender
        skips them. "Sent with a NULL SentDate" therefore means "closed
        without sending".
      - update, by the SendEmail background job: every Pending row, and
        every Error row with AttemptCount below EmailService:MaxAttempts (3),
        of a type the job can build (1 and 2), is sent oldest first, one by
        one. Each attempt adds 1 to AttemptCount and sets LastAttemptDate;
        a success sets EmailStatusID 2, SentDate and clears ErrorMessage; a
        failure sets EmailStatusID 3 and ErrorMessage. SentDate and
        LastAttemptDate are written on the database server's local clock,
        like CreatedDate.

    Column meanings that are not obvious from the type:
      - EmailTypeID    the kind of email - dbo.EmailType.EmailTypeID
                       (1 = Time Log Reminder, 2 = Sheet Submission Reminder,
                       assuming the seed ids - see dbo.EmailType.sql).
      - EmailStatusID  1 = Pending, 2 = Sent, 3 = Error, 4 = Processing
                       (stated in the owner's DDL comments). No lookup table
                       backs these values; Constants.EmailQueue.Status mirrors
                       them in code.
      - SentDate       when the email was successfully sent.
      - LastAttemptDate when sending was last attempted, successful or not.
      - AttemptCount   how many attempts have been made to send it.
      - ErrorMessage   why the last attempt failed.

    Column quirks worth knowing:
      - EmailQueueID is bigint IDENTITY: maps to long in C#.
      - EmailTypeID is int here, but dbo.EmailType.EmailTypeID is tinyint. The
        values fit, but the two map to different C# types (int vs byte).
      - EmailAddress is the only recipient column that is NOT NULL; UserID and
        OrgID are nullable, so an email can go to an address that belongs to
        no user.
      - EmailStatusID defaults to 1 (Pending), AttemptCount to 0.
      - CreatedDate defaults to GETDATE(), which is the SQL Server's LOCAL time,
        not UTC. Every date column is datetime2(0) - whole seconds.
      - EmailAddress and ErrorMessage are varchar, not nvarchar.

    STILL UNCONFIRMED:
      - No foreign keys were declared: not EmailTypeID to dbo.EmailType, nor
        UserID or OrgID to anything. Recorded without them.
      - No indexes beyond the primary key, although a sender would look rows up
        by EmailStatusID.
      - The primary key and default constraints were left unnamed in the DDL,
        so SQL Server generated their names; they are recorded unnamed.
      - Collation of EmailAddress and ErrorMessage - recorded as the database
        default.
*/

IF OBJECT_ID(N'dbo.EmailQueue', N'U') IS NOT NULL
    DROP TABLE dbo.EmailQueue;
GO

CREATE TABLE dbo.EmailQueue
(
    EmailQueueID     bigint          IDENTITY(1,1) PRIMARY KEY,
    UserID           int             NULL,
    EmailAddress     varchar(255)    NOT NULL,
    OrgID            int             NULL,
    EmailTypeID      int             NOT NULL,
    EmailStatusID    tinyint         NOT NULL DEFAULT 1,
    CreatedDate      datetime2(0)    NOT NULL DEFAULT GETDATE(),
    SentDate         datetime2(0)    NULL,
    LastAttemptDate  datetime2(0)    NULL,
    AttemptCount     int             NOT NULL DEFAULT 0,
    ErrorMessage     varchar(2000)   NULL
);
GO
