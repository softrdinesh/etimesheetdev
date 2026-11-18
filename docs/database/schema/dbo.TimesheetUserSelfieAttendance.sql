/*
    Table: dbo.TimesheetUserSelfieAttendance
    Status:   NOT YET APPLIED - the owner runs this file by hand. Change this
              line to "Applied: <date>" once it has been run.
    Recorded: 2026-11-16 as dbo.UserSelfieAttendance (drafted from the
              owner's column list, then settled with the owner)
    Revised:  2026-11-18 - final definition supplied by the owner: table
              renamed to dbo.TimesheetUserSelfieAttendance; ProjectID added
              (int NOT NULL); CheckInLatitude, CheckInLongitude,
              CheckOutLatitude and CheckOutLongitude added (nvarchar(500)
              NULL). The file was renamed to match.

    One row per attendance session of one user on one project: the check-in,
    with its selfie and location, and the check-out with its own selfie and
    location once the session ends. A row with no CheckOutDateTimeUTC is a
    session still open.

    Referenced by:
      dbo.TimesheetProjectTimeLog.AttendanceID (int, no foreign key) - the
      session a time log was logged against.

    Mapped by:
      src/ETimeSheet.Application/Models/Entities/TimesheetUserSelfieAttendance.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/TimesheetUserSelfieAttendanceConfiguration.cs
      (TimesheetProjectRepository, for
      POST api/v1/TimesheetProject/time-sheet-project-check-in-by-selfie,
      which inserts the row, and
      POST api/v1/TimesheetProject/time-sheet-project-check-out-by-selfie,
      which fills in the check-out and LoggedMinutes and adds a
      dbo.TimesheetProjectTimeLog row pointing here. Check-out is same-day
      only, in the user's time zone. The API keeps one open session per user
      per day by looking for it first; the database does not enforce it.)

    Column meanings that are not obvious from the type:
      - CheckInSelfiePath   where the selfie is stored, not the image itself -
        CheckOutSelfiePath  the file lives outside the database. 500 characters
                            is room for a container/blob path, not just a file
                            name.
      - CheckIn/CheckOut    where the device was at check-in / check-out.
        Latitude/Longitude  NULL when the device did not give a location.
      - LoggedMinutes       the session's length in whole minutes, worked out
                            and written at check-out. NULL while the session is
                            open. Stored rather than derived at read time, so a
                            later correction to the check-out time does not
                            silently rewrite history; whatever writes the
                            check-out is responsible for writing this too.

    Column quirks worth knowing:
      - AttendanceID is bigint IDENTITY(1,1), not int: one row per session per
        user per day grows faster than the setup tables. It maps to long in C#.
        dbo.TimesheetProjectTimeLog.AttendanceID, which points here, is only
        int.
      - The latitude and longitude columns are nvarchar(500), not a number or
        geography: the API stores and returns the text it is sent, and nothing
        in the database checks it is a coordinate.
      - The datetime columns are datetime2(0) - whole seconds, no fractions -
        and are UTC, which the column names say out loud. The older tables use
        plain datetime and do not state their zone; this table does not repeat
        that.
      - CreateDate has NO default: whatever writes the row must stamp it, in
        UTC.
      - CreatedBy / UpdatedBy are who wrote the row, which is not necessarily
        UserID: an administrator may correct someone else's attendance.
      - There is no soft-delete column: a removed session would be physically
        deleted. Unlike dbo.TimesheetProjectTimeLog, which keeps IsDelete /
        DeletedDate / DeletedBy, this table does not record that a session was
        removed, or by whom.
      - No foreign keys, matching every other table here: UserID is presumably
        dbo.Signup.UserID, ProjectID dbo.TimesheetProject.ProjectID and
        OrganizationID dbo.Signup.OrganizationID, but nothing in the database
        enforces it.

    Constraints and indexes:
      - The primary key on AttendanceID, clustered, is the only one. It keeps
        the name PK_UserSelfieAttendance from before the table was renamed.
        No index, no unique constraint and no CHECK constraint - the owner's
        decision, so every rule below is the API's to keep:
          * one open session per user - nothing stops a second check-in
            before the first is checked out;
          * a check-out is a time AND a selfie together - nothing stops a row
            claiming a check-out time with no selfie behind it;
          * CheckOutDateTimeUTC >= CheckInDateTimeUTC, and LoggedMinutes >= 0.
        A row written by anything other than the API can break all of these.
      - Reads filtered by UserID, ProjectID or OrganizationID and a date range
        scan the clustered index. That is fine while the table is small; it is
        the first thing to revisit if it grows.

    STILL UNCONFIRMED - questions for the owner:
      - Whether more than one session per user per day is allowed.
      - Whether a selfie path is ever reused or rewritten, and what cleans up
        the files behind a deleted row.
      - The format of the latitude / longitude text (decimal degrees?).
      - Collation of the text columns - recorded as the database default.
*/

IF OBJECT_ID(N'dbo.TimesheetUserSelfieAttendance', N'U') IS NOT NULL
    DROP TABLE dbo.TimesheetUserSelfieAttendance;
GO

CREATE TABLE dbo.TimesheetUserSelfieAttendance
(
    AttendanceID            bigint          IDENTITY(1,1) NOT NULL,
    UserID                  int             NOT NULL,
    ProjectID               int             NOT NULL,
    OrganizationID          int             NOT NULL,

    CheckInDateTimeUTC      datetime2(0)    NOT NULL,
    CheckInSelfiePath       nvarchar(500)   NOT NULL,
    CheckInLatitude         nvarchar(500)   NULL,
    CheckInLongitude        nvarchar(500)   NULL,

    CheckOutDateTimeUTC     datetime2(0)    NULL,
    CheckOutSelfiePath      nvarchar(500)   NULL,
    CheckOutLatitude        nvarchar(500)   NULL,
    CheckOutLongitude       nvarchar(500)   NULL,

    LoggedMinutes           int             NULL,

    CreateDate              datetime2(0)    NOT NULL,
    CreatedBy               int             NOT NULL,

    UpdateDate              datetime2(0)    NULL,
    UpdatedBy               int             NULL,

    CONSTRAINT PK_UserSelfieAttendance
        PRIMARY KEY CLUSTERED (AttendanceID)
);
GO
