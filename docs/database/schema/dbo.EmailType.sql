/*
    Table: dbo.EmailType
    Recorded: 2026-10-08 (DDL and seed rows supplied by the database owner, who
              created and populated the table by hand)

    Lookup table for the kinds of email the application sends. Reference data:
    the rows below are the whole table and are inserted with the schema.

    READ-ONLY TO THE API. Nothing maps or reads it yet. When something does, it
    reads the rows and never adds, edits or deletes one - a new email type is
    added in the database, like every other schema change in this project.

    Mapped by:
      nothing yet.

    Column quirks worth knowing:
      - EmailTypeID is tinyint IDENTITY(1,1): maps to byte in C#, and at most
        255 types.
      - The seed INSERT supplies no ids, so they come from the IDENTITY in
        insertion order: on a fresh table, 1 = 'Time Log Reminder',
        2 = 'Sheet Submission Reminder'. Code that refers to a type by id
        relies on that order.
      - EmailTypeName is varchar(100) and UNIQUE, so it is safe to look a row
        up by name.

    STILL UNCONFIRMED:
      - That the live ids really are 1 and 2 - they would not be if rows were
        inserted and deleted before this seed.
      - The primary key was left unnamed in the DDL, so SQL Server generated its
        name; it is recorded unnamed.
      - Collation of EmailTypeName - recorded as the database default.
*/

IF OBJECT_ID(N'dbo.EmailType', N'U') IS NOT NULL
    DROP TABLE dbo.EmailType;
GO

CREATE TABLE dbo.EmailType
(
    EmailTypeID    tinyint       IDENTITY(1,1) PRIMARY KEY,
    EmailTypeName  varchar(100)  NOT NULL,

    CONSTRAINT UQ_EmailType_EmailTypeName
        UNIQUE (EmailTypeName)
);
GO

INSERT INTO dbo.EmailType (EmailTypeName)
VALUES
    ('Time Log Reminder'),
    ('Sheet Submission Reminder');
GO
