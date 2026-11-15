/*
    Table: dbo.ScheduleType_Lookup
    Recorded: 2026-11-15 - DRAFT, NOT YET APPLIED. Written here on request, to
              be created by hand by the database owner. Change this header to
              "Recorded ... (DDL applied by the database owner)" once it exists
              in the database.

    Lookup table for the schedule types of dbo.SchedulerConfiguration: the
    numeric ScheduleTypeID and its name. Fixed reference data - the five rows
    at the bottom of this file are the whole table and are inserted with the
    schema, so the throwaway test database built from this folder holds them
    too.

    Until now these five values existed only as comments in the owner's DDL
    and as the C# enum below; nothing in the database stated them, and nothing
    stopped dbo.SchedulerConfiguration.ScheduleTypeID holding a 9.

    The values must stay as they are: they are persisted in
    dbo.SchedulerConfiguration.ScheduleTypeID and are the enum's numbers.
      src/ETimeSheet.Shared/Enums/ScheduleType.cs

    Describes:
      ./dbo.SchedulerConfiguration.sql (its ScheduleTypeID column)

    NOT MAPPED, NOT READ. No entity, no IEntityTypeConfiguration and no
    repository reads this table: SchedulerService switches on the
    ScheduleType enum, not on a name read from the database, and nothing
    returns a schedule type to a client. The table is reference data and a
    place to hang a foreign key - it is not a dependency of any code path.
    Because there is no EF mapping, the integration tests' ResetDatabaseAsync
    (which builds its script from the EF model) never touches it, so its seed
    rows survive every reset, as the other lookups' do.

    What this file deliberately does NOT do: add the foreign key from
    dbo.SchedulerConfiguration.ScheduleTypeID to this table. That would be an
    ALTER of an existing table and a separate, deliberate change by the owner -
    and it must not be applied until every existing ScheduleTypeID value is one
    of the five below, or the ALTER fails. The column types already match
    (both tinyint), so nothing here blocks it.

    Column quirks worth knowing:
      - ScheduleTypeID is tinyint, matching dbo.SchedulerConfiguration's
        column, so a foreign key between them needs no conversion. It is NOT
        an IDENTITY: the five values are fixed and are inserted explicitly.
      - ScheduleType is varchar, not nvarchar, matching SchedulerName on
        dbo.SchedulerConfiguration. The names are ASCII and are not shown to
        a user.
      - The names are spelled exactly as the ScheduleType enum's members, so
        the two can be compared by eye.
*/

IF OBJECT_ID(N'dbo.ScheduleType_Lookup', N'U') IS NOT NULL
    DROP TABLE dbo.ScheduleType_Lookup;
GO

CREATE TABLE dbo.ScheduleType_Lookup
(
    ScheduleTypeID  tinyint      NOT NULL,
    ScheduleType    varchar(20)  NOT NULL,

    CONSTRAINT PK_ScheduleType_Lookup
        PRIMARY KEY (ScheduleTypeID),

    CONSTRAINT UQ_ScheduleType_Lookup_ScheduleType
        UNIQUE (ScheduleType)
);
GO

-- The five schedule types. The ids are fixed - they are persisted in
-- dbo.SchedulerConfiguration.ScheduleTypeID and read as the ScheduleType enum -
-- so they are inserted explicitly and must never be renumbered.
INSERT INTO dbo.ScheduleType_Lookup (ScheduleTypeID, ScheduleType)
VALUES
    (1, 'Once'),
    (2, 'Repeated'),
    (3, 'Daily'),
    (4, 'Weekly'),
    (5, 'Monthly');
GO
