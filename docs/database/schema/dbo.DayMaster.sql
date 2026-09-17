/*
    Table: dbo.DayMaster
    Recorded: 2026-09-17 (DDL supplied by the database owner)

    Lookup table for the seven days of the week. Fixed reference data: the seven
    rows below are the whole table and are inserted with the schema.

    READ-ONLY TO THE API. No endpoint, service or repository adds, edits or
    deletes a row here, and there is deliberately no IDayMasterRepository. If a
    day ever has to change, it changes in the database, like every other schema
    object in this project.

    Mapped by:
      src/ETimeSheet.Application/Models/Entities/DayMaster.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/DayMasterConfiguration.cs

    Ids and codes are also declared in
      src/ETimeSheet.Shared/Utilities/Constants.cs  (Constants.DayMaster)
    so that no call site types a 3 or an 'WE' inline.

    Column quirks worth knowing:
      - DayID is NOT an IDENTITY column. The values are supplied by the seed
        INSERT and are meaningful (1 = Monday ... 7 = Sunday, the ISO-8601
        numbering). They are NOT System.DayOfWeek, which numbers Sunday as 0.
      - DayCode is varchar(2), so values come back WITHOUT padding. The day
        columns on dbo.TimesheetMasterSetup are char(2)/char(3) and DO come back
        blank-padded, and Exceptionday is three characters wide where DayCode is
        two. Trim and compare case-insensitively before matching the two.
      - Day and DayCode each carry a UNIQUE constraint, so either one is safe to
        look a row up by.
*/

IF OBJECT_ID(N'dbo.DayMaster', N'U') IS NOT NULL
    DROP TABLE dbo.DayMaster;
GO

CREATE TABLE [dbo].[DayMaster]
(
    [DayID] INT NOT NULL,
    [Day] VARCHAR(20) NOT NULL,
    [DayCode] VARCHAR(2) NOT NULL,

    CONSTRAINT [PK_DayMaster] PRIMARY KEY ([DayID]),
    CONSTRAINT [UQ_DayMaster_Day] UNIQUE ([Day]),
    CONSTRAINT [UQ_DayMaster_DayCode] UNIQUE ([DayCode])
);
GO

INSERT INTO [dbo].[DayMaster] ([DayID], [Day], [DayCode])
VALUES
    (1, 'Monday',    'MO'),
    (2, 'Tuesday',   'TU'),
    (3, 'Wednesday', 'WE'),
    (4, 'Thursday',  'TH'),
    (5, 'Friday',    'FR'),
    (6, 'Saturday',  'SA'),
    (7, 'Sunday',    'SU');
GO
