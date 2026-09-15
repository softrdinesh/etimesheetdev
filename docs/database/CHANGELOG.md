# Database change log

A dated record of what changed in SQL Server, kept by hand. The schema itself is
never changed by this repository - see `README.md`.

Newest first.

---

## 2026-09-15 - `dbo.TimesheetMasterSetup` column list confirmed

The recorded table definition was written from a column dump and carried three
guesses. Two are now settled, by querying the table through the new AdminSetup
endpoints rather than by assumption:

- **There is no `CanUserLoggedPreDayTime` column on the table.** Selecting it
  fails with `Invalid column name 'CanUserLoggedPreDayTime'`. It had been mapped
  on the entity; that mapping is removed.
- `spc_GetTimesheetMasterSetupByUserID` nevertheless **returns** a column of that
  name, and returns it as an `int` holding 0 or 1. The deployed procedure must
  derive the value, so it is mapped only on that procedure's keyless result type
  (`TimesheetMasterSetupDetail`), with `.HasConversion<int>()`.
- The table has exactly the 18 columns recorded in
  `schema/dbo.TimesheetMasterSetup.sql`, and `TimeEntryLockAt` is one of them.

Still unconfirmed: that `SetupID` is an `IDENTITY` column, and that the primary
key is `SetupID` clustered. Inserts through the API do come back with a generated
id, which is consistent with both.

### Rows written by the API

`dbo.TimesheetMasterSetup` is now **written** as well as read, by the AdminSetup
module: insert, update and soft delete (`IsDelete = 1`, plus `DeleteDate` and
`Deletedby`). No schema is touched - rows only.

### One setup per user - enforced in the application, not the database

A user may hold at most one live setup. **There is no unique index on `UserID`
to enforce it**, so the rule lives in `AdminSetupService` and holds only for rows
written through the API; anything inserted directly in SQL can still break it.
It matters because `spc_GetTimesheetMasterSetupByUserID` does not guarantee
uniqueness and the caller takes the first row, so a second row would silently
decide which limits apply. A unique filtered index on
`UserID WHERE IsDelete IS NULL OR IsDelete = 0` would make the database enforce
it properly - worth adding when the existing data is known to be clean.

---

## 2026-09-15 - `spc_GetTimeLoggedDetailsForTask` gained two computed columns

`TotalWorkingHours` and `TotalWorkingMinutes`, both computed with `DATEDIFF` over
the date and the time, so an entry running past midnight measures correctly.
Both are null when any of the four date/time columns behind them is null.

---

## 2026-09-15 - `dbo.TimeLog.IsDeleted` added

`int NOT NULL`, where 1 means deleted - not the `bit` that
`dbo.TimesheetMasterSetup.IsDelete` uses. The two tables genuinely differ, which
is why `TimesheetMasterSetup` does not share `TimeLog`'s `AuditableEntity` base.
