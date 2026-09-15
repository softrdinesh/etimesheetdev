# Database history

This project is **database-first**. The database is the source of truth, and it
is changed by hand, outside this repository.

## The rule

> **No code in this repository ever creates, alters or drops schema.**

There are no EF Core migrations, no `dotnet ef` tooling, no `EnsureCreated`, and
no startup seeding. The API only ever *reads and writes rows* through the objects
described here. If you need a table or a procedure changed, change it in SQL
Server first, then update the files in this folder to match.

## What lives here

```
docs/database/
├── CHANGELOG.md                 dated record of every schema change
├── schema/                      one file per table, as it exists today
│   ├── dbo.TimeLog.sql
│   └── dbo.TimesheetMasterSetup.sql
└── procedures/                  one file per stored procedure / function
    ├── dbo.spc_GetTimeLoggedDetailsForTask.sql
    └── dbo.spc_GetTimesheetMasterSetupByUserID.sql
```

The `.sql` files are **a record of the current state**, not a deployment
mechanism. Nothing in the API executes them.

## Keeping it accurate

These files are what the EF Core mappings in
`src/ETimeSheet.Infrastructure/Data/Configurations/` are written against. If the
real database drifts from what is recorded here, the mapping will be wrong and
queries will fail at runtime with `Invalid column name` — the mismatch will not
be caught at compile time.

So, whenever you change the database:

1. Make the change in SQL Server.
2. Update the matching file in `schema/` or `procedures/`.
3. Add a dated entry to `CHANGELOG.md` saying what changed and why.
4. Tell me, so the entity, its `IEntityTypeConfiguration<T>` and any affected
   repository method are updated to match.

## Where the real schema comes from

To regenerate a table's definition from the live database, script it from SSMS
or Azure Data Studio (`Script Table as → CREATE To`), or list the columns with:

```sql
EXEC sp_help 'dbo.TimeLog';

SELECT name, type_desc FROM sys.objects
WHERE type IN ('U','P','FN','IF','TF')
ORDER BY type_desc, name;
```
