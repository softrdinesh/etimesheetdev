# Database change log

A dated record of what changed in SQL Server, kept by hand.

**Nothing in this repository ever changes the database.** The owner applies
every change manually; this file, and the `.sql` files beside it, are the
record of what they applied. See `README.md` for the rule and the routine.

Newest first.

> Restored 2026-09-21. This file was deleted by accident in commit `418f344`
> (2026-09-17) and the changes made between then and now were recorded only in
> the headers of the individual `.sql` files. The entries below that date are
> reconstructed from those headers and from the Git history of this folder, so
> they describe what the files show; where a change was never written down
> anywhere, it says so.

---

## 2026-09-21 - `dbo.Signup` recorded, PARTIALLY

`schema/dbo.Signup.sql` is new, and is **not** the live table's DDL - it
records only the six columns this codebase can see (`UserID`, `Name`, `Email`,
`RoleID`, `OrganizationID`, `CountryID`). The real table is older than this API
and has never been scripted for us.

It was added because both recorded procedures join `dbo.Signup`, and the
integration fixture replays this folder into a throwaway container: without the
file, every integration test failed with `Invalid object name 'dbo.Signup'`.

**Outstanding:** ask the database owner to script the real table and replace
the guess. Until then, production is right and the file is wrong.

---

## 2026-09-21 - `spc_GetEmployeeListByPOrgID` returns `CountryID`

`s.CountryID` added as the **last** column of the SELECT; nothing else in the
body changed. It is `dbo.Signup`'s column, not the setup's, so it is present
even for an employee with no timesheet setup at all - unlike every other
nullable column the procedure returns, which is null because the LEFT JOIN
found no setup.

Being last matters: `EmployeeListDetail` is mapped by position-independent name,
but the JSON contract writes properties in declaration order.

---

## 2026-09-21 - `dbo.Country` recorded, and `spc_GetTimesheetMasterSetupByUserID` replaced

- `schema/dbo.Country.sql` recorded from the column list supplied by the
  database owner. `TimeZone` holds one IANA zone id, or several comma-separated
  when the country spans more than one. `CreateDate` is `datetimeoffset(7)`,
  unlike the plain `datetime` on `TimeLog` and `TimesheetMasterSetup`.
- `data/dbo.Country_UpdateTimeZone.sql` added: a one-off populate of
  `Country.TimeZone` from the IANA tzdata `zone.tab`, matched on `Code`. It
  widens the column to 1000 characters first, because the US value alone is 598.
  **Run by hand, once, by the database owner** - the integration fixture does
  not execute anything under `data/`.
- `procedures/dbo.spc_GetTimesheetMasterSetupByUserID.sql` replaced with the
  deployed body. Three changes from what had been recorded: it now JOINs
  `dbo.Signup` and returns `s.CountryID`, it returns `tms.TimeZone`, and
  `CanUserLoggedPreDayTime` is **derived** from `GETDATE()` against
  `tms.TimeEntryLockAt` rather than selected. That JOIN is an INNER one, so a
  setup whose `UserID` has no `Signup` row now returns no rows at all.
- `schema/dbo.TimesheetMasterSetup.sql` gained `CountryID` and `TimeZone`.

---

## 2026-09-19 - `spc_GetEmployeeListByPOrgID` recorded

The employee-list procedure, as handed over by the database owner - every
employee in one organisation with their contracted time, what they logged in
the current Monday-Sunday week, and the resulting progress.

Recorded as `CREATE OR ALTER`: it was supplied as `ALTER PROCEDURE`, which
cannot run against the empty database the integration fixture builds.

Its header documents the traps in the body - the `Signup.RoleID = 2` definition
of "employee", the LEFT JOIN that keeps setup-less employees, the `GETDATE()`
week that no test clock can move, and the hours/minutes pair that is a quotient
and a remainder rather than two independent figures. Read it before changing
anything in there.

---

## 2026-09-17 - `dbo.DayMaster` added, and the setup's day columns changed type

- `schema/dbo.DayMaster.sql`: new lookup table for the seven days, with its
  seven rows. `DayID` is **not** an identity - the values are meaningful
  (1 = Monday ... 7 = Sunday, ISO-8601, *not* `System.DayOfWeek`).
- `dbo.TimesheetMasterSetup.StartDay` / `EndDay` / `Exceptionday` changed from
  `char(2)` / `char(2)` / `char(3)` to `int`, per the column list supplied by
  the database owner. They now hold `DayMaster.DayID` values.

No FOREIGN KEY was declared between them, so a setup row can legally hold an id
the lookup does not contain; the API reads an unknown id as "no week
configured" rather than failing the request.

**Never established:** whether the rows that held char day codes before this
change were converted to the matching ids or left as they were.

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
