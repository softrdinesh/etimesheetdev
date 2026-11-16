/* =============================================================================
   dbo.ScheduleType_Lookup -- create the table and populate its five rows
   -----------------------------------------------------------------------------
   Run by     : the database owner, by hand. This is the runnable script.
                The RECORD of the table lives at
                ../schema/dbo.ScheduleType_Lookup.sql and begins with
                DROP TABLE -- do not run that one.
   Purpose    : names the values of dbo.SchedulerConfiguration.ScheduleTypeID,
                which until now existed only as comments and as the C# enum
                src/ETimeSheet.Shared/Enums/ScheduleType.cs.
   Idempotent : safe to re-run. The table is created only if it is missing, and
                only the rows that are not already there are inserted. Nothing
                is dropped, deleted or overwritten, so a re-run cannot lose a
                row you edited.
   Destructive: no. It creates one new table and inserts into it. No existing
                table is altered, and dbo.SchedulerConfiguration is not touched.

   The ids are fixed: they are persisted in
   dbo.SchedulerConfiguration.ScheduleTypeID and are read as the ScheduleType
   enum's numbers. Never renumber them.

   NOT included on purpose -- the foreign key from
   dbo.SchedulerConfiguration.ScheduleTypeID to this table. That is an ALTER of
   an existing table and a separate decision, and it fails unless every
   ScheduleTypeID already in dbo.SchedulerConfiguration is one of the five
   below. The last query in this script tells you whether it would.
   ============================================================================= */

SET NOCOUNT ON;
GO

/* ---------------------------------------------------------------------------
   1. The table. Created only when it does not already exist.
       - ScheduleTypeID is tinyint to match
         dbo.SchedulerConfiguration.ScheduleTypeID, so a foreign key between
         the two would need no conversion.
       - It is NOT an IDENTITY: the five values are fixed and inserted
         explicitly.
       - ScheduleType is varchar to match SchedulerName on the same table. The
         names are spelled exactly as the C# enum's members.
   --------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ScheduleType_Lookup', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScheduleType_Lookup
    (
        ScheduleTypeID  tinyint      NOT NULL,
        ScheduleType    varchar(20)  NOT NULL,

        CONSTRAINT PK_ScheduleType_Lookup
            PRIMARY KEY (ScheduleTypeID),

        CONSTRAINT UQ_ScheduleType_Lookup_ScheduleType
            UNIQUE (ScheduleType)
    );

    PRINT 'dbo.ScheduleType_Lookup created.';
END
ELSE
BEGIN
    PRINT 'dbo.ScheduleType_Lookup already exists; left as it is.';
END
GO

/* ---------------------------------------------------------------------------
   2. The five rows. Only the missing ones are inserted, matched on
      ScheduleTypeID, so a re-run adds nothing and changes nothing.
   --------------------------------------------------------------------------- */
INSERT INTO dbo.ScheduleType_Lookup (ScheduleTypeID, ScheduleType)
SELECT seed.ScheduleTypeID, seed.ScheduleType
FROM (VALUES
        (CONVERT(tinyint, 1), 'Once'),
        (CONVERT(tinyint, 2), 'Repeated'),
        (CONVERT(tinyint, 3), 'Daily'),
        (CONVERT(tinyint, 4), 'Weekly'),
        (CONVERT(tinyint, 5), 'Monthly')
     ) AS seed (ScheduleTypeID, ScheduleType)
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.ScheduleType_Lookup AS existing
    WHERE existing.ScheduleTypeID = seed.ScheduleTypeID
);

PRINT CONCAT(@@ROWCOUNT, ' schedule type(s) inserted.');
GO

/* ---------------------------------------------------------------------------
   3. Check. The first result is the finished table - five rows, 1 to 5.
   --------------------------------------------------------------------------- */
SELECT ScheduleTypeID, ScheduleType
FROM dbo.ScheduleType_Lookup
ORDER BY ScheduleTypeID;

/* The second result should be EMPTY. Any row it returns is a schedule a job is
   configured with that this lookup does not name - the API already refuses to
   schedule it ("ScheduleTypeID N is not a known schedule type"), and it is also
   what would make the optional foreign key fail. */
SELECT  configuration.SchedulerConfigurationID,
        configuration.SchedulerName,
        configuration.ScheduleTypeID
FROM dbo.SchedulerConfiguration AS configuration
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.ScheduleType_Lookup AS lookups
    WHERE lookups.ScheduleTypeID = configuration.ScheduleTypeID
)
ORDER BY configuration.SchedulerConfigurationID;
GO
