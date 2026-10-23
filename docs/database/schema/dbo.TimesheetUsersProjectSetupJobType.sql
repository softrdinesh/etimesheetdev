/*
    Table: dbo.TimesheetUsersProjectSetupJobType
    Recorded: 2026-10-23 (written at the database owner's request, to be
              created by hand; confirm here once it has been applied)

    Lookup table for dbo.Timesheet_Users_Project_setup.Jobtype - where a user
    works on a project they are assigned to. Reference data: the rows below
    are the whole table and are inserted with the schema.

    READ-ONLY TO THE API. Nothing maps or reads it yet: the API stores and
    returns Jobtype as a plain number and does not check it against this
    table.

    Mapped by:
      nothing yet.

    Column quirks worth knowing:
      - JobTypeID is NOT an IDENTITY: the ids are written explicitly, because
        they are the values stored in dbo.Timesheet_Users_Project_setup.Jobtype:
          1 = Work from office
          2 = Work from home
      - JobType is nvarchar(50) and UNIQUE, so it is safe to look a row up by
        name.
      - No foreign key from dbo.Timesheet_Users_Project_setup.Jobtype to this
        table is declared.
*/

IF OBJECT_ID(N'dbo.TimesheetUsersProjectSetupJobType', N'U') IS NOT NULL
    DROP TABLE dbo.TimesheetUsersProjectSetupJobType;
GO

CREATE TABLE dbo.TimesheetUsersProjectSetupJobType
(
    JobTypeID  int           NOT NULL,
    JobType    nvarchar(50)  NOT NULL,

    CONSTRAINT PK_TimesheetUsersProjectSetupJobType
        PRIMARY KEY CLUSTERED (JobTypeID),
    CONSTRAINT UQ_TimesheetUsersProjectSetupJobType_JobType
        UNIQUE (JobType)
);
GO

INSERT INTO dbo.TimesheetUsersProjectSetupJobType (JobTypeID, JobType)
VALUES
    (1, N'Work from office'),
    (2, N'Work from home');
GO
