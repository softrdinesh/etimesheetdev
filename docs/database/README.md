# Database history

This project is **database-first**. The database is the source of truth, it is
changed **by hand, by its owner, outside this repository**, and this folder is
the record of what they changed.

## The rule

> **No code in this repository ever creates, alters or drops schema, and
> neither does anyone working in it. Schema changes are made manually in SQL
> Server; this folder only writes down what was made.**

There are no EF Core migrations, no `dotnet ef` tooling, no `EnsureCreated`, no
startup seeding and no deployment step that touches schema. The API only ever
*reads and writes rows* through the objects described here.

The `.sql` files are **a record of the current state, not a deployment
mechanism.** Do not run them against PPMUAT, production, or anyone's local
database - most of them start with `DROP TABLE`.

### The one exception, and its fence

The integration suite replays this folder into a **throwaway SQL Server
container** that Testcontainers creates and destroys inside a single test run
(`ETimeSheetApiFactory.CreateSchemaAsync`). That is the only code anywhere that
executes these files, and it is the only place they may be executed.

What keeps it honest:

- `AssertUsingThrowawayContainer` refuses to run unless the host resolved
  exactly the container's connection string, so the suite cannot be pointed at
  a real database even by a configuration mistake.
- `ArchitectureRuleTests.NothingButTheTestContainerFixtureExecutesSchema` fails
  the build if any other file under `src/` or `tests/` starts issuing DDL.

Anything under `data/` is excluded even from that: the fixture reads `schema/`
and `procedures/` only.

## What lives here

```
docs/database/
├── schema/                      one file per table, as it exists today
│   ├── dbo.Country.sql
│   ├── dbo.CountryCurrency_Lookup.sql
│   ├── dbo.DayMaster_Lookup.sql
│   ├── dbo.EmailQueue.sql
│   ├── dbo.EmailType.sql
│   ├── dbo.SchedulerConfiguration.sql
│   ├── dbo.TimeLog.sql
│   ├── dbo.Timesheet_Approver_setup.sql
│   ├── dbo.Timesheet_role_assignment.sql
│   ├── dbo.Timesheet_Users_Project_setup.sql
│   ├── dbo.TimeSheet_Weekend_setup.sql
│   ├── dbo.Timesheetclientsetup.sql
│   ├── dbo.TimesheetHolidaysetup.sql
│   ├── dbo.TimesheetMasterSetup.sql
│   ├── dbo.TimesheetProject.sql
│   ├── dbo.TimesheetProjectBudget.sql
│   ├── dbo.TimesheetProjectCategory.sql
│   ├── dbo.TimesheetProjectStatus.sql
│   ├── dbo.TimesheetProjectSubmissionStatus.sql
│   ├── dbo.TimesheetSubmission.sql
│   ├── dbo.TimeSheetSubmissionAuditLog.sql
│   ├── dbo.TimesheetUserRole.sql
│   └── dbo.TimesheetUsersProjectSetupJobType_Lookup.sql
├── procedures/                  one file per stored procedure / function
│   ├── dbo.spc_AddEditClientSetup.sql
│   ├── dbo.spc_AddEditTimesheetHolidaySetup.sql
│   ├── dbo.spc_AddEditTimesheetProject.sql
│   ├── dbo.spc_AddEditTimesheetProjectBudget.sql
│   ├── dbo.spc_AddEditTimesheetProjectCategory.sql
│   ├── dbo.spc_AddEditTimesheetApproverSetup.sql
│   ├── dbo.spc_AddEditTimesheetProjectStatus.sql
│   ├── dbo.spc_AddEditTimesheetWeekendSetup.sql
│   ├── dbo.spc_AddEditUserAccessSetup.sql
│   ├── dbo.spc_AddEditUserRoleByAdmin.sql
│   ├── dbo.spc_GetAdminDashboardSummaryByOrgID.sql
│   ├── dbo.spc_GetClientListForMasterAdmin.sql
│   ├── dbo.spc_GetEmployeeListByPOrgID.sql
│   ├── dbo.spc_GetOrganizationList.sql
│   ├── dbo.spc_GetSubmittedSheetList.sql
│   ├── dbo.spc_GetTimeLoggedDetailsForTask.sql
│   ├── dbo.spc_GetTimesheetApproverSetup.sql
│   ├── dbo.spc_GetTimesheetHolidaySetup.sql
│   ├── dbo.spc_GetTimesheetMasterSetupByUserID.sql
│   ├── dbo.spc_GetTimesheetProject.sql
│   ├── dbo.spc_GetTimesheetProjectBudget.sql
│   ├── dbo.spc_GetTimesheetProjectBudgetByOrgID.sql
│   ├── dbo.spc_GetTimesheetProjectCategory.sql
│   ├── dbo.spc_GetTimesheetProjectStatus.sql
│   ├── dbo.spc_GetTimesheetUserDetailsByUserID.sql
│   ├── dbo.spc_GetTimesheetUserListByOrgID.sql
│   ├── dbo.spc_GetTimesheetWeekendSetup.sql
│   ├── dbo.spc_GetUserAccessSetup.sql
│   ├── dbo.spc_GetUserDashboardSummaryByUserID.sql
│   ├── dbo.spc_GetUserRoleListByOrgID.sql
│   ├── dbo.spc_GetUsersTaskList.sql
│   └── dbo.spc_Signup_timesheet_user.sql
└── data/                        one-off data scripts, run by hand, never by the tests
    ├── dbo.Country_UpdateTimeZone.sql
    ├── dbo.CountryCurrency_Lookup_Insert.sql
    └── dbo.TimesheetProjectStatus_Insert.sql
```

There is no `CHANGELOG.md` yet: each file's `Recorded:` / `Revised:` header
lines are the history today. `dbo.Signup` is **not** recorded at all - the
procedures that join it say so in their headers, and the test container builds
it from the EF mapping (`SignupConfiguration`).

A procedure whose header says **`Status: DRAFT`** has been written here but not
yet created in a database by the owner; the endpoint that calls it answers 500
until it is.

Each file opens with a header giving `Recorded:` (and `Revised:`) dates, what
maps it, what reads or writes it, and the quirks worth knowing before touching
it. That header is part of the record - keep it current with the DDL below it.

A file may be a **partial** recording when the live definition has never been
supplied; it must say so at the top, in those words, and list what it leaves
out. None is partial today.

## Keeping it accurate

These files are what the EF Core mappings in
`src/ETimeSheet.Infrastructure/Data/Configurations/` are written against. If the
real database drifts from what is recorded here, the mapping will be wrong and
queries will fail at runtime with `Invalid column name` - the mismatch will not
be caught at compile time.

So, whenever the database changes:

1. **You** make the change in SQL Server. Nothing here does it for you, and
   nobody should ask an agent to run it.
2. Update the matching file in `schema/` or `procedures/`, header included.
3. Add a dated entry to `CHANGELOG.md` saying what changed and why.
4. Say so, so the entity, its `IEntityTypeConfiguration<T>` and any affected
   repository method are updated to match.

Steps 2-4 are the part that belongs in this repository. Recording a change that
has not actually been applied is worse than recording nothing: the mappings will
be written against a database that does not exist.

## Where the real schema comes from

To regenerate a table's definition from the live database, script it from SSMS
or Azure Data Studio (`Script Table as → CREATE To`), or list the columns with:

```sql
EXEC sp_help 'dbo.TimeLog';

SELECT name, type_desc FROM sys.objects
WHERE type IN ('U','P','FN','IF','TF')
ORDER BY type_desc, name;
```
