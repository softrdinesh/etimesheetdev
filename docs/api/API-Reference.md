# ETimeSheet API Reference

Every endpoint the API exposes today, with its purpose, input payload, success
response and failure responses — each with a worked example.

Generated from the code on branch `develop` (2026-10-20). If you change a
controller, DTO or validator, change this file with it.

---

## Contents

- [Conventions](#conventions)
  - [Base URL](#base-url)
  - [Authentication](#authentication)
  - [The response envelope](#the-response-envelope)
  - [JSON rules](#json-rules)
  - [Empty results are successes](#empty-results-are-successes)
  - [Error contract](#error-contract)
- [TimeLog](#timelog)
  - [Logged Time List](#1-logged-time-list)
  - [Employee Timesheet Setup](#2-employee-timesheet-setup)
  - [Save Time Log (Add / Edit)](#3-save-time-log-add--edit)
  - [Task List](#4-task-list)
  - [User Dashboard Summary](#10-user-dashboard-summary)
- [Admin](#admin)
  - [Save Timesheet Setup](#5-save-timesheet-setup)
  - [Admin Timesheet Setup](#6-admin-timesheet-setup)
  - [Delete Timesheet Setup](#7-delete-timesheet-setup)
  - [Employee List](#8-employee-list)
  - [Country Time Zone List](#9-country-time-zone-list)
  - [Admin Dashboard Summary](#11-admin-dashboard-summary)
  - [User Role List](#16-user-role-list)
  - [Add / Edit User Role](#17-add--edit-user-role)
  - [Delete User Role](#18-delete-user-role)
  - [Project Category List](#22-project-category-list)
  - [Add / Edit Project Category](#23-add--edit-project-category)
  - [Remove Project Category](#24-remove-project-category)
- [SheetSubmission](#sheetsubmission)
  - [Submit Timesheet](#12-submit-timesheet)
  - [Review Timesheet](#13-review-timesheet)
  - [Current Week Sheet Details](#14-current-week-sheet-details)
  - [Submitted Sheet List](#15-submitted-sheet-list)
- [MasterAdmin](#masteradmin)
  - [The `ClientResponse` shape](#the-clientresponse-shape)
  - [Client List](#19-client-list)
  - [Add / Edit Client Setup](#20-add--edit-client-setup)
  - [Remove Client Setup](#21-remove-client-setup)
  - [Organization List](#32-organization-list)
- [TimesheetProject](#timesheetproject)
  - [Project List](#25-project-list)
  - [Add / Edit Project](#26-add--edit-project)
  - [Project Status List](#27-project-status-list)
  - [Add / Edit Project Status](#33-add--edit-project-status)
  - [Remove Project Status](#34-remove-project-status)
  - [Remove Project](#28-remove-project)
  - [Project Budget List](#29-project-budget-list)
  - [Add / Edit Project Budget](#30-add--edit-project-budget)
  - [Remove Project Budget](#31-remove-project-budget)
  - [Organisation Project Budget List](#35-organisation-project-budget-list)
  - [Create Timesheet User](#36-create-timesheet-user)
  - [Edit Timesheet User](#37-edit-timesheet-user)
  - [Remove Timesheet User](#38-remove-timesheet-user)
  - [Organisation Timesheet User List](#39-organisation-timesheet-user-list)
  - [Holiday List](#40-holiday-list)
  - [Add / Edit Holiday](#41-add--edit-holiday)
  - [Remove Holiday](#42-remove-holiday)
  - [Weekend Setup List](#43-weekend-setup-list)
  - [Add / Edit Weekend Setup](#44-add--edit-weekend-setup)
  - [Remove Weekend Setup](#45-remove-weekend-setup)
  - [User Access Setup List](#46-user-access-setup-list)
  - [Add / Edit User Access Setup](#47-add--edit-user-access-setup)
  - [Remove User Access Setup](#48-remove-user-access-setup)
  - [Project timesheets: periods and sheet codes](#project-timesheets-periods-and-sheet-codes)
  - [Project Time Log List](#49-project-time-log-list)
  - [Add / Edit Project Time Log](#50-add--edit-project-time-log)
  - [Remove Project Time Log](#51-remove-project-time-log)
  - [Project Timesheet List](#52-project-timesheet-list)
  - [Submit Project Timesheet](#53-submit-project-timesheet)
  - [Approve / Reject Project Timesheet](#54-approve--reject-project-timesheet)
- [Health endpoints](#health-endpoints)
- [Enumerations](#enumerations)
- [Endpoint summary table](#endpoint-summary-table)
- [Known gaps](#known-gaps)

---

## Conventions

### Base URL

| Environment | URL |
|---|---|
| Local (HTTPS) | `https://localhost:7041` |
| Local (HTTP) | `http://localhost:5041` |

Swagger UI is served at `/swagger` (document at `/swagger/v1/swagger.json`)
**in every environment**, deliberately, while security is off. It goes back
behind a flag when JWT is switched back on.

Route shape: the controller carries the prefix `api/v1/[controller]`, the action
carries a kebab-case, verb-first route.

```
POST https://localhost:7041/api/v1/TimeLog/save-employee-time-log
```

### Authentication

**Authentication is currently switched off.** Every controller is marked
`[AllowAnonymous]`, so no `Authorization` header is required or read.

The JWT stack is still registered and intact. When it is turned back on:

- `AllowAnonymous` comes off `AdminController` first — it is the surface
  that most needs a permission behind it.
- The `userId` / `createdBy` / `deletedBy` fields marked **temporary** below
  disappear from the payloads and are taken from the token claims instead.

There is also **no fallback authorization policy** (`AddAuthorizationServices`
registers plain `AddAuthorization()`), so a URL that matches no route is a plain
**404**, not a 401. When JWT comes back, `options.FallbackPolicy =
options.DefaultPolicy` is restored in the same change that removes
`[AllowAnonymous]`.

Until then, a caller can claim to be any user. Do not expose this API publicly.

### CORS

Browser callers are limited to the origins in `Cors:AllowedOrigins`
(configuration), with credentials allowed. An empty list allows no browser
origin at all — that is the default, not a misconfiguration.

### The response envelope

Every endpoint — success or failure — returns the same envelope:

```jsonc
{
  "success": true,        // did the request succeed
  "message": "",          // human-readable outcome; "" on plain reads
  "data": { },            // the payload, or null when there is none
  "errors": []            // failure detail; always empty on success
}
```

### JSON rules

| Rule | Effect |
|---|---|
| Property naming | **camelCase** (`sheetId`, `totalWorkingHours`) |
| Nulls | **Always written** (`DefaultIgnoreCondition = Never`). Every field in the tables below is present in every response; a nullable one with no value comes back as `null`, never missing. The response shape does not depend on the data. |
| Enums | Written as **names** (`"Save"`, `"Draft"`). On input both the name `"Save"` and the number `1` are accepted. |
| Times of day **in** | A JSON **string** in exactly `hh:mm:ss` — e.g. `"08:00:00"`. **Not** a number, and not a looser spelling: `"8:00:00"`, `"08:00"`, `"1.08:00:00"`, `"-08:00:00"` and `"08:00:00.0000000"` are all rejected. Must be `00:00:00`–`23:59:59` (the columns are SQL `time(7)`). |
| Times of day **out** | The same `"hh:mm:ss"` string, so a value can be read from one endpoint and sent straight to another. |
| `DateTime` | ISO-8601. Values read from the database carry no offset (`"2026-09-01T08:15:02.443"`); audit stamps set during the same request are UTC and carry a `Z` (`"2026-09-17T10:22:41.9801234Z"`). Date-only columns ignore any time part. |
| Content type | `application/json` in and out. |
| Cancellation | Every action honours client disconnect; an aborted request answers **499**. |

### Empty results are successes

**An endpoint that finds no data returns `200` with `success: true` and
`data: null` (or an empty list).** It does not return `404`, and it never
returns `success: false`.

`success: false` means *the caller has something to fix* — a malformed payload,
a broken rule, a collision, a defect. "There is no row for this user" is none of
those: the request was well formed, it ran, and the answer is that there is
nothing there. A client that sees `success: false` goes looking for its own
mistake, and on these endpoints there isn't one — finding an employee who has
not been set up yet is often the *reason* the screen was opened.

| Endpoint kind | No data looks like |
|---|---|
| Returns a **list** | `success: true`, `data: []` |
| Returns an **object** | `success: true`, `data: null`, and a `message` saying so |

So **branch on `data`, not on the status code**, for Logged Time List, Employee Timesheet Setup, Task List, User Dashboard Summary, Admin Timesheet Setup, Employee List (`employees: []`) and Country Time Zone List.

This does not soften the **write** endpoints. Asking to delete a setup that does
not exist, or to save one against a country id that does not exist, is a failed
*operation*, not an empty *result* — those still fail, because reporting them as
successes would tell a client that something happened when nothing did.

### Error contract

| Thrown | HTTP | When |
|---|---|---|
| `ValidationException` / FluentValidation failure | **400** | The payload is malformed — wrong shape, missing field, out-of-range value |
| `BusinessException` | **400** | The payload is well formed but breaks a rule that needed the database to check |
| `UnauthorizedException` | **401** | Not authenticated *(unreachable today — auth is off)* |
| `ForbiddenException` | **403** | Authenticated but not allowed *(unreachable today)* |
| `NotFoundException` | **404** | A **write** named a row that does not exist. Never used for a read that simply found nothing — see [Empty results are successes](#empty-results-are-successes) |
| `ConflictException` | **409** | The request collides with data already stored |
| `ExternalServiceException` | **502** | A service this API calls (the document upload service) failed or answered with something unusable. The request was fine; nothing was saved |
| anything else | **500** | A defect. Logged in full; the caller gets a correlation id only |

**Validation failure (400)** — shape-level, produced before the action runs:

```json
{
  "success": false,
  "message": "One or more validation errors occurred.",
  "data": null,
  "errors": [
    "UserId: UserId is required: an entry must belong to an employee.",
    "Status: Status must be 1 (Save) or 2 (Draft)."
  ]
}
```

Each entry is `"<FieldName>: <message>"`. Failures found by a **validator** are
all reported at once, as above. A `ValidationException` thrown by a **service** —
every time-of-day format failure is one — carries a single entry, because the
service stops at the first thing it cannot accept.

**Business-rule failure (400)** — the payload was fine, the rule was not:

```json
{
  "success": false,
  "message": "2026-09-13 is a Sunday, which is not a working day in this user's timesheet week (Monday to Friday).",
  "data": null,
  "errors": []
}
```

Note the difference: shape failures fill `errors[]`, rule failures put a single
sentence in `message` and leave `errors[]` empty.

**Not found (404)** — only ever from a write naming a row that is not there:

```json
{
  "success": false,
  "message": "Timesheet setup '4242' was not found.",
  "data": null,
  "errors": []
}
```

**Conflict (409):**

```json
{
  "success": false,
  "message": "This overlaps time log 8812, which already covers 09:00 to 12:30 that day.",
  "data": null,
  "errors": []
}
```

**Bad gateway (502)** — an external service failed; the request itself was fine:

```json
{
  "success": false,
  "message": "The document service could not be reached.",
  "data": null,
  "errors": []
}
```

**Server error (500)** — never a stack trace. In Production:

```json
{
  "success": false,
  "message": "An unexpected error occurred. Please try again later.",
  "data": null,
  "errors": ["CorrelationId: 0HN7GQ2K1V9PA:00000003"]
}
```

In Development the exception message replaces the generic one, to speed up
debugging. The correlation id matches the `CorrelationId` on the logged error.

---

## TimeLog

Controller: `src/ETimeSheet.Api/Controllers/TimeLogController.cs`
Service: `TimeLogService` · Prefix: `/api/v1/TimeLog`

---

### 1. Logged Time List

`GET /api/v1/TimeLog/get-logged-time-list/{userId}`

**Purpose** — Return every entry a user has logged, across all tasks and all
dates, **most recently logged first** (the procedure orders by `CreateDate`,
not by the day the time was logged against). Soft-deleted entries are left out.

Replaced `POST /api/v1/TimeLog/get-time-logged-details` on 2026-09-25. There is
no task filter, no date range and no `summary` block any more.

#### Route parameter

| Parameter | Type | Rules | Notes |
|---|---|---|---|
| `userId` | int | `>= 1` (route constraint) | **Temporary** — moves to the token when auth is on |

#### Example request

```
GET https://localhost:7041/api/v1/TimeLog/get-logged-time-list/101
```

#### Success response — `200 OK`

`data` is an array of `TimeLoggedDetailResponse`:

| Field | Type | Meaning |
|---|---|---|
| `sheetId` | int | The entry's key — send it as `timeLogId` to edit the entry |
| `sheetCode` | string? | The generated code of this user's timesheet week — `SHT0001`, `SHT0002`, … — shared by every entry the user logged in that week. Null on a row created before generation existed (until it is next edited); a legacy row may instead hold a hand-entered reference such as `TS-00121` |
| `taskId` | int? | The task the time was logged against |
| `isProjectTask` | bool? | `true` for a project task, `false` for a sprint task; null on rows written before the column existed |
| `description` | string? | What was worked on |
| `startDate` | date? | Day the work started |
| `startTime` | string? | Clock time it started, `hh:mm:ss` |
| `endDate` | date? | Day the work ended |
| `endTime` | string? | Clock time it ended, `hh:mm:ss` |
| `status` | enum? | `"Save"` (1) or `"Draft"` (2) |
| `statusName` | string | `"Save"` / `"Draft"`; `""` when the column is null |
| `totalWorkingHours` | decimal? | This entry's duration in hours, rounded to 2 dp by the procedure but written at the procedure's scale — e.g. `3.500000` |
| `totalWorkingMinutes` | int? | This entry's duration in whole minutes |

#### Example response

```json
{
  "success": true,
  "message": "",
  "data": [
    {
      "sheetId": 8815,
      "sheetCode": null,
      "taskId": 55,
      "isProjectTask": null,
      "description": "Code review and follow-up fixes.",
      "startDate": "2026-09-15T00:00:00",
      "startTime": "10:00:00",
      "endDate": "2026-09-15T00:00:00",
      "endTime": "18:00:00",
      "status": "Draft",
      "statusName": "Draft",
      "totalWorkingHours": 8.000000,
      "totalWorkingMinutes": 480
    },
    {
      "sheetId": 8812,
      "sheetCode": "SHT0121",
      "taskId": 55,
      "isProjectTask": true,
      "description": "Implemented the save-employee-time-log endpoint.",
      "startDate": "2026-09-14T00:00:00",
      "startTime": "09:00:00",
      "endDate": "2026-09-14T00:00:00",
      "endTime": "12:30:00",
      "status": "Save",
      "statusName": "Save",
      "totalWorkingHours": 3.500000,
      "totalWorkingMinutes": 210
    }
  ],
  "errors": []
}
```

`sheetCode` and `isProjectTask` are `null` on the first entry rather than
missing: every field is written on every row, so a client can index into the
response without checking whether a key exists.

#### Empty result

A user with no entries is **not** an error — a `200` with `data: []`.

#### Error responses

| Status | Cause | Notes |
|---|---|---|
| **404** | `userId` is not a positive integer | Route constraint — the route does not match, so there is no envelope |
| **500** | Unhandled defect | generic message + correlation id |

---

### 2. Employee Timesheet Setup

`GET /api/v1/TimeLog/get-timesheet-setup-by-user/{userId}`

**Purpose** — Return the timesheet setup that governs one user: their maximum
loggable time, contract type, the bounds of their timesheet week, an
informational back-dating flag (see the warning below), and the time zone their
day is measured in.

This is the **timesheet-screen** view — a nine-column projection from
`spc_GetTimesheetMasterSetupByUserID`. It is deliberately *not* the same shape as
the administrative view returned by `/api/v1/Admin/get-user-timesheet-setup/{userID}`.
Two audiences, two contracts.

#### Route parameters

| Parameter | Type | Constraint |
|---|---|---|
| `userId` | int | `:int:min(1)` — a value below 1 does not match the route and returns **404** |

No request body.

#### Example request

```http
GET /api/v1/TimeLog/get-timesheet-setup-by-user/101
```

#### Success response — `200 OK`

`data` is a `TimesheetMasterSetupResponse`:

| Field | Type | Meaning |
|---|---|---|
| `setupId` | int | The setup row's key |
| `maxTimeLoggedByUserInHours` | string? | From `MaxTimeinhrs`. `"08:00:00"` — `hh:mm:ss`, not a number |
| `maxTimeLoggedByUserInMinutes` | string? | From `MaxTiminmins`, `hh:mm:ss` |
| `contractType` | int? | Contract type id |
| `startDay` | int? | First day of the timesheet week — a `dbo.DayMaster.DayID`, 1 = Monday … 7 = Sunday |
| `endDay` | int? | Last day of the timesheet week — a `dbo.DayMaster.DayID` |
| `canUserLoggedPreDayTime` | bool? | `true` when the database server's current time is at or before `TimeEntryLockAt`; `false` after it, **or when `TimeEntryLockAt` is null**. Informational only — Save Time Log does not read it. Held as 0/1, surfaced as `true`/`false`. See the warning below |
| `countryId` | int? | The user's country, from **`dbo.Signup.CountryID`** — the person's country, not the setup row's own `CountryID` |
| `timeZone` | string? | The IANA zone the user's timesheet day is measured in — one id, e.g. `"Asia/Kolkata"`. `null` for a setup saved before time zones existed, in which case the API falls back to UTC |

> `countryId` and `timeZone` were added to the procedure on **2026-09-21**.

#### Example response

```json
{
  "success": true,
  "message": "",
  "data": {
    "setupId": 17,
    "maxTimeLoggedByUserInHours": "08:00:00",
    "maxTimeLoggedByUserInMinutes": "00:30:00",
    "contractType": 1,
    "startDay": 1,
    "endDay": 5,
    "canUserLoggedPreDayTime": true,
    "countryId": 91,
    "timeZone": "Asia/Kolkata"
  },
  "errors": []
}
```

> ### ⚠ Two things about `canUserLoggedPreDayTime`
>
> The procedure derives it as
> `CASE WHEN CAST(GETDATE() AS TIME) <= tms.TimeEntryLockAt THEN 1 ELSE 0 END`,
> and that has two consequences a client should know:
>
> 1. **It is measured on the database server's clock**, not the employee's. It
>    answers "has the cut-off passed *where the server is*". The write path does
>    **not** use it at all: `save-employee-time-log` judges `timeEntryLockAt`
>    itself in the employee's own zone.
> 2. **A `null` `TimeEntryLockAt` yields `false`, not `true`** — comparing
>    against `NULL` is `UNKNOWN`, so the `CASE` falls to `ELSE 0`. A setup that
>    has never had a cut-off configured therefore *reports* "may not back-date",
>    but the write path ignores the flag and applies **no** lock for that user.

#### When the user has no setup

**`200`, `success: true`, `data: null`** — not a 404. The read succeeded; the
answer is that there is no row. This also covers a user with no `dbo.Signup`
row, since the procedure inner-joins the two and either absence returns nothing.

```json
{
  "success": true,
  "message": "This user has no timesheet setup.",
  "data": null,
  "errors": []
}
```

> **`data: null` is not "no limits — log whatever you like."** It means the user
> has not been configured, and
> [Save Time Log](#3-save-time-log-add--edit) refuses to log time
> for such a user. Do not read absent as zero, or as unlimited. A setup that
> *exists* but has empty columns comes back as an **object** with nulls inside
> it, which is a different answer again.

#### Error responses

| Status | Cause |
|---|---|
| **404** | `userId` below 1 — no route matches. This is routing, not "no data" |
| **500** | Unhandled defect |

> If the data holds more than one setup row for a user, the first is returned and
> a warning is logged. The procedure does not guarantee uniqueness.

> **This read and Save Time Log do not read the setup the same way.** This read
> does not skip a soft-deleted setup, so a user whose only setup was deleted gets
> an object here and is still refused by Save Time Log. With several rows, this
> read returns whichever the procedure yields first, while the save validates
> against the live row with the lowest `setupId`.

---

### 3. Save Time Log (Add / Edit)

`POST /api/v1/TimeLog/save-employee-time-log`

**Purpose** — Log one block of time for an employee, or edit one they already
logged. One endpoint, one payload, for both:

| `timeLogId` | What happens |
|---|---|
| `0`, or left out | A new entry is **added** and returned with its generated `sheetId` and `sheetCode` |
| the `sheetId` of a live entry | That entry is **edited** — overwritten with the payload — and returned |

An edit is checked against **every rule an add is**, plus one more: the entry
*as it stands* must still be open to change. See
[Editing, and the cut-off](#editing-and-the-cut-off).

Before the row is stored it is checked against the user's timesheet setup (their
working week, their `timeEntryLockAt` cut-off if one is set, their daily maximum) and against
the entries they already have that day, so two blocks cannot cover the same hour.
**A user with no setup cannot log time at all.**

#### Request body — `TimeLogSaveRequest`

| Field | Type | Required | Rules | Notes |
|---|---|---|---|---|
| `timeLogId` | int | no | `>= 0` | `0` or omitted adds an entry. The `sheetId` of an existing entry edits it |
| `userId` | int | yes | `> 0` | The employee the time belongs to; also selects the setup it is validated against. On an edit it must be the entry's owner. **Temporary** |
| `taskId` | int | yes | `> 0` | Time is always logged against a task |
| `isProjectTask` | bool | yes | `true` or `false`; not `null`, not omitted | `true` when `taskId` is a project task, `false` when it is a sprint task — the two lists [Task List](#4-task-list) returns, whose ids can coincide |
| `description` | string? | no | unbounded (`nvarchar(max)`) | What was worked on |
| `startDate` | date | yes | not empty | Day the work started. Time part ignored |
| `endDate` | date? | no | `>= startDate` and `<= startDate + 1 day` | Defaults to `startDate`. Exists only for a shift running past midnight |
| `startTime` | string | yes | `hh:mm:ss`, `00:00:00`–`23:59:59` | Clock time it started — e.g. `"09:00:00"` |
| `endTime` | string | yes | `hh:mm:ss`, `00:00:00`–`23:59:59`, and after `startTime` once both dates are counted | Clock time it ended |
| `status` | enum | yes | `1`/`"Save"` or `2`/`"Draft"` | An entry with no status is neither saved nor drafted |
| `createdBy` | int | yes | `> 0` | Who is recording the entry — not necessarily `userId`, since a manager may log on someone's behalf. Written to `createdBy` on an add and to **`updatedBy` on an edit**; an edit never changes who created the entry. **Temporary** |

> **`sheetCode` is not in the payload.** The service generates it — see below.
> Sending one is ignored: the property does not exist on the request.

Duration is measured across **both ends including their dates**, so an overnight
entry (22:00 Monday → 06:00 Tuesday) is valid, while 17:00 → 09:00 on a single
day is not.

#### The generated `sheetCode`

Every saved entry gets a reference, and the caller neither sends one nor chooses
one. It comes back on the response.

**A code names one user's timesheet week, not one entry.** The week is anchored
on the setup's `startDay` and runs seven calendar days from it (so days after
`endDay`, such as an exception day, belong to the same week):

- The **first entry a user logs in a week** — whichever day it is logged
  against — takes the next code in the sequence.
- Every **later entry by that user in the same week** reuses that code.
- The **next week** opens a new code on its first entry.

Example, week Monday → Friday: the user first logs on Tuesday (Monday is already
locked) → `SHT0007` is opened. Wednesday, Thursday and Friday entries all get
`SHT0007`. The following Monday's entry opens `SHT0008`, used until that week ends.
Another user in the same week has their own code.

- An **edit** keeps the entry's code while it stays in the same week. An edit
  that moves the entry to a different week gives it that week's code (opening one
  if it is the first entry there).
- A setup with no usable `startDay` falls back to a Monday-anchored week.
- If the user's week already holds a row with a hand-entered reference (e.g.
  `TS-00121`), later entries that week reuse that reference rather than opening
  an `SHT` code.
- An edit of a row with no code (logged before generation existed) gives it its
  week's code.

```
SHT0001  SHT0002  SHT0003  …  SHT9998  SHT9999
                                 ↓  the width runs out
SHT00001 SHT00002 SHT00003  …  SHT99998 SHT99999
                                 ↓
SHT000001 …
```

- The first entry ever generated is **`SHT0001`**. An empty table, or one holding
  only hand-entered references such as `TS-00121`, both start there — a
  reference that is not `SHT` followed by digits names no position in the sequence
  and is skipped.
- A week's first entry takes the **next number after the highest generated
  code**, read from the table at save time.
- When a width runs out, the sequence **starts a new generation one digit wider,
  back at 1**: `SHT9999` is followed by `SHT00001`, `SHT99999` by `SHT000001`.

**The limit can never be reached.** Each generation holds nine times as many
codes as the one before, and because the widths differ, a code from one
generation can never equal a code from another — `SHT0001` and `SHT00001` are
different strings. The width grows on demand up to the twelve digits
`varchar(15)` can hold after the `SHT` prefix, which is 10¹² codes.

Two details worth knowing:

- **Soft-deleted entries still count.** A deleted row has spent its code: it is
  never issued to another week, and if it was the week's first entry, the rest
  of that user's week keeps using it.
- **A rejected request consumes nothing.** The code is read after every rule has
  passed, so a 400 or a 409 leaves no gap in the sequence.

#### Example request — add an entry

```json
{
  "timeLogId": 0,
  "userId": 101,
  "taskId": 55,
  "isProjectTask": true,
  "description": "Implemented the save-employee-time-log endpoint.",
  "startDate": "2026-09-16T00:00:00",
  "startTime": "09:00:00",
  "endTime": "12:30:00",
  "status": "Save",
  "createdBy": 101
}
```

#### Example request — overnight shift

```json
{
  "userId": 101,
  "taskId": 55,
  "isProjectTask": true,
  "description": "Overnight release support.",
  "startDate": "2026-09-16T00:00:00",
  "endDate": "2026-09-17T00:00:00",
  "startTime": "22:00:00",
  "endTime": "06:00:00",
  "status": 1,
  "createdBy": 102
}
```

`status` accepts the number as well as the name.

#### Example request — edit an entry

```json
{
  "timeLogId": 8931,
  "userId": 101,
  "taskId": 55,
  "isProjectTask": true,
  "description": "Implemented the save endpoint, and its edit path.",
  "startDate": "2026-09-16T00:00:00",
  "startTime": "09:00:00",
  "endTime": "13:00:00",
  "status": "Save",
  "createdBy": 101
}
```

Send the **whole** entry, not just what changed: an edit overwrites every field
in the payload, so a field left out is cleared (or, for `endDate`, reset to
`startDate`).

#### Success response — `200 OK`

`data` is a `TimeLogResponse` — the entry exactly as it was stored, so a client
that has just logged time can display it back without a second request. The
`message` says which happened: `"Time logged."` for an add, `"Time log
updated."` for an edit.

| Field | Type | Meaning |
|---|---|---|
| `sheetId` | int | The entry's key — generated on an add; send it back as `timeLogId` to edit |
| `sheetCode` | string? | **Generated by the service** — `SHT0001`, `SHT0002`, … — once per user per timesheet week and shared by every entry in that week. The caller does not send one and cannot choose one. An edit keeps the code unless it moves the entry to another week |
| `taskId` | int? | As sent |
| `isProjectTask` | bool? | As sent. Nullable only because rows logged before the column existed hold `null` |
| `description` | string? | As sent |
| `userId` | int? | As sent |
| `startDate` | date? | As sent, date only |
| `startTime` | string? | As sent, `hh:mm:ss` |
| `endDate` | date? | **Always written**, even if omitted — defaults to `startDate`, because a row with no end date cannot have its duration computed |
| `endTime` | string? | As sent, `hh:mm:ss` |
| `status` | enum? | `"Save"` or `"Draft"` |
| `statusName` | string | The status spelled out, so clients need not carry the numbers |
| `totalWorkingHours` | decimal | Duration in hours, rounded to at most 2 dp — trailing zeros are not written (`3.5`, `8`, `1.67`). Computed across both ends including dates, so an overnight entry measures correctly instead of coming out negative |
| `createdBy` | int? | Who added the entry. Unchanged by an edit |
| `createDate` | datetime? | When the entry was added, UTC. Unchanged by an edit |
| `updatedBy` | int? | Who last edited the entry — `createdBy` from the edit's payload. `null` until it has been edited |
| `updateDate` | datetime? | When the entry was last edited, UTC. `null` until it has been edited |

#### Example response

```json
{
  "success": true,
  "message": "Time logged.",
  "data": {
    "sheetId": 8931,
    "sheetCode": "SHT0007",
    "taskId": 55,
    "isProjectTask": true,
    "description": "Implemented the save-employee-time-log endpoint.",
    "userId": 101,
    "startDate": "2026-09-16T00:00:00",
    "startTime": "09:00:00",
    "endDate": "2026-09-16T00:00:00",
    "endTime": "12:30:00",
    "status": "Save",
    "statusName": "Save",
    "totalWorkingHours": 3.5,
    "createdBy": 101,
    "createDate": "2026-09-16T11:04:22.1172345Z",
    "updatedBy": null,
    "updateDate": null
  },
  "errors": []
}
```

> On an **add**, `createDate` is the UTC time stamped by the API and carries a
> trailing `Z` with up to 7 fractional digits. On an **edit**, `createDate` is
> the stored `datetime` value (no `Z`, ~3 ms precision) and `updateDate` carries
> the `Z`.

#### Error responses

**400 — shape validation** (`errors[]` populated):

| Trigger | Message |
|---|---|
| `timeLogId < 0` | `TimeLogId must be 0 to add an entry, or the id of the entry to edit.` |
| `userId <= 0` | `UserId is required: an entry must belong to an employee.` |
| `taskId <= 0` | `TaskId is required: time is always logged against a task.` |
| `isProjectTask` missing or `null` | `IsProjectTask is required: true for a project task, false for a sprint task.` |
| `createdBy <= 0` | `CreatedBy is required: the row records who logged or edited the time.` |
| `startDate` empty | `StartDate is required.` |
| `status` not 1 or 2 | `Status must be 1 (Save) or 2 (Draft).` |
| `endDate` before `startDate` | `EndDate must not be earlier than StartDate.` |
| `endDate` more than a day later | `EndDate must be the same day as StartDate or the day after it.` |

The three time rules are checked by `TimeLogService` rather than the validator,
because one parser decides what a time of day is for the whole API. They are
reported in the same 400 envelope, but **one at a time** — the service stops at
the first — where the shape failures above are reported together:

| Trigger | Message |
|---|---|
| `startTime` missing or blank | `StartTime is required, as a time of day in hh:mm:ss format - for example "09:00:00".` |
| `startTime` malformed or out of range | `StartTime must be a time of day in hh:mm:ss format, between "00:00:00" and "23:59:59" - for example "09:00:00".` |
| `endTime` missing or blank | `EndTime is required, as a time of day in hh:mm:ss format - for example "09:00:00".` |
| `endTime` malformed or out of range | `EndTime must be a time of day in hh:mm:ss format, between "00:00:00" and "23:59:59" - for example "09:00:00".` |
| entry runs backwards or has no duration | `EndTime must be after StartTime, once both dates are taken into account.` |

```json
{
  "success": false,
  "message": "One or more validation errors occurred.",
  "data": null,
  "errors": [
    "Status: Status must be 1 (Save) or 2 (Draft).",
    "TaskId: TaskId is required: time is always logged against a task."
  ]
}
```

A time failure arrives on its own, from the service:

```json
{
  "success": false,
  "message": "One or more validation errors occurred.",
  "data": null,
  "errors": [
    "EndTime: EndTime must be after StartTime, once both dates are taken into account."
  ]
}
```

**400 — business rules** (single sentence in `message`, `errors[]` empty):

| Rule | Example message |
|---|---|
| Edit names someone else's entry | `Time log '8931' does not belong to user '102', so it cannot be edited as theirs.` |
| No setup for the user | `User '4242' has no timesheet setup, so there are no limits to check this entry against. An administrator has to create one before they can log time.` |
| Sheet already submitted | `Timesheet 'SHT0007' has already been submitted, so its time can no longer be added to or changed.` |
| Future date | `Time cannot be logged against 2026-12-01 because it has not happened yet.` |
| Back-dating cut-off passed | `The cut-off for logging time against an earlier day is 18:00 Asia/Kolkata time, and it is now 19:42 there, so 2026-09-15 is locked.` |
| Entry starts before a cut-off that has passed | `It is 22:10 Asia/Kolkata time, past the 21:00 cut-off, so time starting before 21:00 can no longer be added or changed. Ask an administrator to record it for you.` |
| Non-working day | `2026-09-13 is a Sunday, which is not a working day in this user's timesheet week (Monday to Friday).` |
| Over the daily maximum | `Logging 3.5 hours would bring 2026-09-16 to 9.5 hours, above the 8 hour daily maximum in this user's timesheet setup (6 hours are already logged).` |

```json
{
  "success": false,
  "message": "Logging 3.5 hours would bring 2026-09-16 to 9.5 hours, above the 8 hour daily maximum in this user's timesheet setup (6 hours are already logged).",
  "data": null,
  "errors": []
}
```

Hour figures in these messages are rounded to at most 2 dp with no trailing
zeros (`3.5`, `8`, `1.67`).

The daily maximum counts **every live entry on that date, drafts included** — a
draft still occupies the time, and excluding drafts would let a user reach any
total by drafting first.

"That day" — for both the daily maximum and the overlap check below — means
entries that **start** on the same date. An overnight entry from the previous
day is not checked for overlap against the next morning, and an overnight
entry's whole duration counts toward the day it starts.

**404 — the entry to edit does not exist** — `timeLogId` names no row, or a
soft-deleted one (a deleted entry cannot be edited back to life):

```json
{
  "success": false,
  "message": "Time log '8931' was not found.",
  "data": null,
  "errors": []
}
```

**409 — overlap** with an entry already stored that day:

```json
{
  "success": false,
  "message": "This overlaps time log 8812, which already covers 09:00 to 12:30 that day.",
  "data": null,
  "errors": []
}
```

A conflict rather than a validation error: the payload is well formed, and it is
the rows already in the table that make it impossible. **Touching ends do not
overlap** — an entry ending at 12:00 and the next starting at 12:00 are adjacent,
which is how a day is normally filled in. On an edit, the entry being edited is
left out of the check, so it cannot overlap itself.

**500** — unhandled defect.

#### The `timeEntryLockAt` cut-off, and the employee's time zone

**Every rule about a time of day is judged on the employee's own clock**, read
from the `TimeZone` on their timesheet setup — never on the server's. A cut-off
written as `21:00` means nine in the evening *where the employee is sitting*;
for a team in Kolkata that moment is 15:30 UTC, and comparing it against the
server's clock would lock them out five and a half hours early.

Once that moment has passed, the employee can **still log the hours they are
working** — what they can no longer do is add a block that *started* before it:

| Local now | Entry starts | Result |
|---|---|---|
| 12:00 | 10:00 | logged — the cut-off has not arrived |
| 19:00 | 17:00 | logged |
| 21:00 exactly | 19:00 | logged — the deadline is the last moment that works, not the first that does not |
| 22:00 | 22:00 | logged — the entry is *after* the cut-off |
| 22:00 | 19:00 | **400** — the forgotten evening block needs an administrator |

An entry's **start** is what places it, so a block running 20:00–23:00 began
before a 21:00 cut-off and is refused with the rest of the evening; it is not
split at the cut-off.

**A setup with no `timeEntryLockAt` (`null`) has no lock at all**: today and any
earlier day can be logged or edited at any time of day. Only a future date is
still refused. When `timeEntryLockAt` holds a value, an earlier day is open until
that time of day in the employee's zone and locked after it.

#### Editing, and the cut-off

**Before the cut-off, every entry of the day can be added or edited. After it,
an entry that started before the cut-off can be neither added nor edited** —
only entries starting after it can.

An edit is therefore checked **twice**: once for where the entry *is*, and once
for where it is *going*. Checking only the new values would let a locked 19:00
block be dragged to 22:00 after a 21:00 cut-off — which is editing locked time
with a different end result.

| Local now | Entry is now | Edited to | Result |
|---|---|---|---|
| 19:00 | 17:00–18:00 | 17:00–18:30 | edited — the cut-off has not arrived |
| 22:00 | 22:00–23:00 | 22:00–23:30 | edited — the entry is after the cut-off |
| 22:00 | 22:00–23:00 | 19:00–20:00 | **400** — the new start is before the passed cut-off |
| 22:00 | 19:00–20:00 | 22:00–23:00 | **400** — the entry as it stands is locked |
| 22:00 | 19:00–20:00 | 19:00–20:30 | **400** — locked |

The same applies to the date: when a cut-off is set, editing an entry on an
earlier day needs that day to be open for **both** its current date and its new
one. With no cut-off, earlier days are never locked.

A half-recorded entry is judged on what it has: with no stored date it has no
day to be locked, and with no stored start nothing to place against the
cut-off, so the edit that completes it is not refused for the gap it fixes.

> **A missing or unrecognised `timeZone` falls back to UTC** and is logged as a
> warning rather than refusing the entry — matching how the rest of this API
> treats a half-filled setup. Every setup saved before the column existed is
> this case, so those users behave exactly as they did before.

"Today" and "the future" are read on the same clock. At 09:00 in Auckland it is
still yesterday in UTC, so judging those on the server's date would reject an
employee logging the morning they are actually living through.

#### Rule evaluation order

Rules are applied in this order, and the first failure answers:

1. Payload shape (FluentValidation) → **400**
2. `startTime`, then `endTime`, parsed as `hh:mm:ss` → **400**
3. The entry runs forwards once both dates are counted → **400**
4. *Edit only:* `timeLogId` names a live entry → **404**, owned by `userId` → **400**
5. User has a timesheet setup → **400**
6. The sheet is not submitted — *on an edit*, the sheet the entry is on now; and the sheet of the week the entry lands in, the edited entry excluded. Any row in `dbo.TimesheetSubmission` for the code closes it, whatever its status → **400**
7. *Edit only:* the entry as it stands is still open — its current date passes rule 8 and its current start passes rule 10 → **400**
8. Date is open for logging — not in the future; if back-dated and `timeEntryLockAt` is set, it has not passed **in the employee's zone** (a `null` `timeEntryLockAt` never locks) → **400**
9. Date falls inside the timesheet week, or is the exception day → **400**
10. The entry does not start before a `timeEntryLockAt` that has already passed **in the employee's zone** → **400**
11. No overlap with existing entries that day, the edited entry excluded → **409**
12. Day stays within the daily maximum, the edited entry excluded → **400**

> A week that is not configured, or configured with codes the application does
> not recognise, imposes **no** constraint — blocking an employee over a
> half-filled setup they cannot fix would be worse. `exceptionDay` is allowed
> **in addition** to the week, and the week may wrap (a Sunday-to-Thursday week
> is normal in some of this data).

---

### 4. Task List

`GET /api/v1/TimeLog/get-user-task-list-by-userid/{userId}`

**Purpose** — Return the tasks a user owns, as **two separate lists**: project
tasks and sprint tasks. This is what the time-entry screen picks a `taskId`
from — send the list the task came from as `isProjectTask` on
[Save Time Log](#3-save-time-log-add--edit).

Backed by `spc_GetUsersTaskList`, which returns two result sets: project tasks
from `dbo.TaskMaster`, then sprint tasks from `dbo.SprintTaskManagement`, each
filtered on `Taskowner`.

The route is matched without regard to case, so `…/get-user-task-list-by-userID/101`
works too.

#### Route parameters

| Parameter | Type | Constraint |
|---|---|---|
| `userId` | int | `:int:min(1)` — a value below 1 does not match the route and returns **404** |

No request body.

#### Example request

```http
GET /api/v1/TimeLog/get-user-task-list-by-userid/101
```

#### Success response — `200 OK`

`data` is a `UserTaskListResponse`:

| Field | Type | Meaning |
|---|---|---|
| `projectTasks` | `UserTaskResponse[]` | Tasks from `dbo.TaskMaster` the user owns |
| `sprintTasks` | `UserTaskResponse[]` | Tasks from `dbo.SprintTaskManagement` the user owns |

Each `UserTaskResponse`:

| Field | Type | Meaning |
|---|---|---|
| `taskId` | int | `TaskMaster.TaskID` in `projectTasks`; `SprintTaskManagement.SprintTaskID` in `sprintTasks` |
| `taskName` | string? | The task's name |

> **A `taskId` is unique only within its own list.** The two lists come from
> different tables, so the same number can appear in both and mean two
> different tasks. That is what `isProjectTask` on a time log disambiguates.

#### Example response

```json
{
  "success": true,
  "message": "",
  "data": {
    "projectTasks": [
      { "taskId": 55, "taskName": "Timesheet API" },
      { "taskId": 61, "taskName": "Country time zones" }
    ],
    "sprintTasks": [
      { "taskId": 55, "taskName": "Sprint 14 - code review" }
    ]
  },
  "errors": []
}
```

#### When the user owns no tasks

**`200`**, with both lists empty — never `null`, and never a 404:

```json
{
  "success": true,
  "message": "",
  "data": { "projectTasks": [], "sprintTasks": [] },
  "errors": []
}
```

#### Error responses

| Status | Cause |
|---|---|
| **404** | `userId` below 1 — no route matches. This is routing, not "no data" |
| **500** | Unhandled defect |

---

### 10. User Dashboard Summary

`GET /api/v1/TimeLog/get-user-dashboard-summary-by-userID/{userId}`

**Purpose** — One user's dashboard figures: time logged today and this timesheet
week, the week's expected time, the percentage logged, and the time still
pending.

Backed by `spc_GetUserDashboardSummaryByUserID`. Numbered 10 because it was
added after the Admin endpoints; it belongs to the TimeLog controller.

The route is matched without regard to case, so
`…/get-user-dashboard-summary-by-userid/101` works too.

#### Route parameters

| Parameter | Type | Constraint |
|---|---|---|
| `userId` | int | `:int:min(1)` — a value below 1 does not match the route and returns **404** |

No request body.

#### How the figures are worked out

- **The week** starts on the setup's `startDay` and runs seven calendar days —
  the same week a `sheetCode` is shared across. No usable `startDay` means a
  Monday week. Entries on days after `endDay` (an exception day, say) count as
  logged time.
- **Expected** = working days from `startDay` to `endDay` (wrapping past Sunday)
  × the setup's daily time. The exception day adds nothing to it.
- **Logged** counts every live entry, `Save` and `Draft` alike. An entry crossing
  midnight or the week boundary counts only the part inside the day or week.
- **Percentage** = logged ÷ expected, two decimals, capped at `100`.
- **Pending** = expected − logged, never below `0`.

> **"Today" is the database server's date**, not the employee's. For anyone in a
> different time zone from the server, `todayLogged*` — and on the first day of
> the week, the week itself — is off for part of every day.

#### Success response — `200 OK`

`data` is a `UserDashboardSummaryResponse`:

| Field | Type | Meaning |
|---|---|---|
| `userId` | int | The user |
| `name` | string? | From `dbo.Signup` |
| `weekStartDate` | date | First day of the current timesheet week |
| `weekEndDate` | date | Last day — always six days after `weekStartDate` |
| `todayLoggedMinutes` | int | Minutes logged today |
| `todayLoggedText` | string? | The same, as text — `"7h 30m"`, `"0h"` |
| `weekLoggedMinutes` | int | Minutes logged this week |
| `weekLoggedText` | string? | The same, as text |
| `weekExpectedMinutes` | int? | Minutes the setup expects this week. **`null` with no (or an incomplete) setup** |
| `weekExpectedText` | string? | The same, as text — `"40h"` |
| `weekLoggedPercentage` | decimal? | Logged ÷ expected, 0–100, two decimals. `null` when expected is null or 0 |
| `weekPendingMinutes` | int? | Still to log this week, never negative. `null` when expected is |
| `weekPendingText` | string? | The same, as text |

A user with no timesheet setup still gets their logged figures; the five
expected / percentage / pending fields (`weekExpectedMinutes`, `weekExpectedText`,
`weekLoggedPercentage`, `weekPendingMinutes`, `weekPendingText`) are `null` — there is nothing to measure
against, which is not the same as 0%.

#### Example request

```http
GET /api/v1/TimeLog/get-user-dashboard-summary-by-userID/101
```

#### Example response

```json
{
  "success": true,
  "message": "",
  "data": {
    "userId": 101,
    "name": "Asha Patel",
    "weekStartDate": "2026-09-21T00:00:00",
    "weekEndDate": "2026-09-27T00:00:00",
    "todayLoggedMinutes": 270,
    "todayLoggedText": "4h 30m",
    "weekLoggedMinutes": 1650,
    "weekLoggedText": "27h 30m",
    "weekExpectedMinutes": 2400,
    "weekExpectedText": "40h",
    "weekLoggedPercentage": 68.75,
    "weekPendingMinutes": 750,
    "weekPendingText": "12h 30m"
  },
  "errors": []
}
```

#### When the user does not exist

**`200`**, `success: true`, `data: null` — as on
[Employee Timesheet Setup](#2-employee-timesheet-setup). The procedure returns no
row when the user is not in `dbo.Signup`, or their `isdelete` there is anything
but `0` (`NULL` included).

```json
{
  "success": true,
  "message": "This user was not found.",
  "data": null,
  "errors": []
}
```

#### Error responses

| Status | Cause |
|---|---|
| **404** | `userId` below 1 — no route matches. This is routing, not "no data" |
| **500** | Unhandled defect |

---

## Admin

Controller: `src/ETimeSheet.Api/Controllers/AdminController.cs`
Service: `AdminService` · Prefix: `/api/v1/Admin`

Administrative CRUD over `dbo.TimesheetMasterSetup`, plus the organisation's
employee list, its dashboard summary, the country/time-zone lookup, the
organisation's user roles (`dbo.TimesheetUserRole`), and its project
categories (`dbo.TimesheetProjectCategory`). **This is
the surface that most obviously needs a permission behind it** — it has none
while authentication is off, and **Employee List** and **Admin Dashboard
Summary** return organisation-wide data to anyone who asks.

---

### 5. Save Timesheet Setup

`POST /api/v1/Admin/save-user-timesheet-setup`

**Purpose** — Save a user's timesheet setup: adding or updating, whichever
applies. One payload, one endpoint, for all three cases.

**There is no setup id in the payload.** A user holds exactly one setup, so
`userId` is what names the row. Send the same payload every time:

| Existing state | What happens |
|---|---|
| Live row for that user | Updated in place; `updatedBy` / `updateDate` stamped |
| Soft-deleted row for that user | Overwritten **and revived** — `isDelete`, `deleteDate`, `deletedBy` cleared; `updatedBy` / `updateDate` stamped; the original `createdBy` / `createDate` are left alone |
| Nothing | Inserted; `createdBy` / `createDate` stamped |

The response carries the saved row with its `setupId`, so the caller never needs
a second call to find out which happened — and cannot create a duplicate row by
sending the wrong id.

> The deleted-row case is why a revived setup keeps its original key: a user whose
> setup was deleted would otherwise look like a new user and get a second row.

#### Request body — `AdminSaveRequest`

**Almost everything is required.** As of 2026-09-22 the only optional fields
are `exceptionDay`, `timeEntryLockAt`, `needToSendReminder` and
`reminderTimeBeforeCutoff`; every other field in the table below
must be sent with a usable value. The columns behind them are nearly all
nullable, so this is a contract decision, not a database one — a half-filled
setup is a setup nothing downstream can compute a week from.

`countryId` is required **even though its column is nullable** — a timesheet
setup that names no country is not a setup anyone asked for. It is required, not
validated: the id is stored exactly as sent, and no check confirms a country
with that id exists.

| Field | Type | Required | Rules |
|---|---|---|---|
| `userId` | int | yes | `> 0` — identifies the row to save |
| `maxTimeInHrs` | string? | **yes** | `hh:mm:ss`, **`00:00:00`–`23:00:00`** — e.g. `"08:00:00"`. The *hours* half of the daily maximum |
| `maxTimInMins` | string? | **yes** | `hh:mm:ss`, **`00:00:00`–`00:59:00`** — e.g. `"00:30:00"`. The *minutes* that go with `maxTimeInHrs`, so it can never carry an hour of its own |
| `organizationId` | int? | **yes** | `> 0` |
| `contractType` | int? | **yes** | exactly `1` (Full Time) or `2` (Part Time). Nothing else — a `3` is refused, not stored as an unnamed contract |
| `startDay` | int? | **yes** | a `dbo.DayMaster.DayID`, `1`–`7` (1 = Monday … 7 = Sunday) |
| `endDay` | int? | **yes** | a `dbo.DayMaster.DayID`, `1`–`7` |
| `exceptionDay` | int? | no | a `dbo.DayMaster.DayID`, `1`–`7`. A day worked *in addition* to the normal week. **The only optional day field** |
| `countryId` | int? | **yes** | `> 0`. A `dbo.Country.ID`. **Stored as sent** — its existence is not checked |
| `timeZone` | string? | **yes** | One IANA zone id, e.g. `"America/New_York"`. **Stored as sent.** Must carry a value — `null`, `""` and whitespace are all refused — and be ≤ 100 chars |
| `timeEntryLockAt` | string? | no | `hh:mm:ss`, `00:00:00`–`23:59:59`. Time of day after which entry is locked — measured in the setup's `timeZone`. A moment in the day, so the narrower bounds above do not apply |
| `needToSendReminder` | bool? | no | Whether the user is sent a reminder to log time. Absent is stored as `null` — no reminder |
| `reminderTimeBeforeCutoff` | string? | no | `hh:mm:ss`, `00:00:00`–`23:59:59`. How long **before** `timeEntryLockAt` the reminder goes out — a duration, so `"00:30:00"` is 30 minutes. Absent is stored as `null` |
| `createdBy` | int | yes | `> 0`. Lands in `CreatedBy` on insert, `UpdatedBy` on update/revive. **Temporary** |

> `canUserLoggedPreDayTime` is deliberately **absent** from this payload — it is
> not a column on `dbo.TimesheetMasterSetup`, it is derived by
> `spc_GetTimesheetMasterSetupByUserID`. There is nothing here to store.

> The day fields were two- and three-letter codes (`"MO"`, `"SUN"`) until
> 2026-09-17. They are `dbo.DayMaster` ids now, and are validated against the
> exact range `1`–`7` rather than for shape, because the lookup fixes the
> vocabulary at seven rows.

> **Where each rule is enforced, and why you get one time error at a time.**
> Everything except the three time fields is checked by the validator, which
> reports **all** its failures together in `errors[]`. The time fields are read
> by `AdminService` through the single `TimeOfDay` parser — being required and
> being in range both need the value parsed, so they cannot be stated in a
> validator that is forbidden from parsing. Service checks stop at the first
> failure, so a payload with two bad times reports the first one only, and you
> will see the second after fixing it. Validator failures are returned before
> the action runs, so time-field errors only appear once every validator failure
> is fixed.

#### `countryId` and `timeZone` are stored verbatim

**Neither is looked up, derived or cross-checked.** `dbo.Country` is not read on
this path at all — whatever the payload carries is what lands in the row, on
insert and on edit alike.

| You send | What gets stored |
|---|---|
| `countryId: 233`, `timeZone: "America/Chicago"` | Exactly that pair |
| `countryId: 232`, `timeZone: "America/Chicago"` | Exactly that pair — the mismatch is **not** rejected |
| `countryId: 999999` (no such country) | `999999` — existence is **not** checked |
| `timeZone` omitted, `null`, `""` or `"  "` | **400** `TimeZone is required.` — nothing is stored |

So **the caller owns the pairing.** Build the choice from
[Country Time Zone List](#9-country-time-zone-list), which returns
every country/zone pair with the zone spelled the way `dbo.Country` spells it,
and send its `countryId` and `timeZone` back unchanged.

> **Changed 2026-09-22.** This endpoint used to *resolve* `timeZone` from the
> country: a single-zone country supplied its own zone and ignored whatever you
> sent, and a multi-zone country required `timeZone` to be one of its own or
> answered 400. That is gone, along with the 404 for an unknown `countryId` and
> the 400 for a country with no zones. Three failure modes fewer — and one
> guarantee fewer: a stored zone is no longer necessarily one its country has.

> **A zone that names nothing real degrades quietly.** Nothing validates the id
> against the system zone database, so a misspelling is stored happily and
> [Save Time Log](#3-save-time-log-add--edit) then falls back to
> **UTC** when it cannot resolve it — logging a warning, not failing. An
> employee's cut-off would be judged on the wrong clock. Send ids from
> [Country Time Zone List](#9-country-time-zone-list) and this cannot happen.

#### Example request

```json
{
  "userId": 101,
  "maxTimeInHrs": "08:00:00",
  "maxTimInMins": "00:30:00",
  "organizationId": 3,
  "contractType": 1,
  "startDay": 1,
  "endDay": 5,
  "exceptionDay": 7,
  "countryId": 91,
  "timeZone": "Asia/Kolkata",
  "timeEntryLockAt": "18:00:00",
  "needToSendReminder": true,
  "reminderTimeBeforeCutoff": "00:30:00",
  "createdBy": 9
}
```

`"Asia/Kolkata"` is stored because that is what was sent — the country is not
consulted. See [`countryId` and `timeZone` are stored verbatim](#countryid-and-timezone-are-stored-verbatim).

#### Minimal request

Only `exceptionDay`, `timeEntryLockAt`, `needToSendReminder` and
`reminderTimeBeforeCutoff` can be left out, so the minimum is
almost the whole payload:

```json
{
  "userId": 101,
  "maxTimeInHrs": "08:00:00",
  "maxTimInMins": "00:30:00",
  "organizationId": 3,
  "contractType": 1,
  "startDay": 1,
  "endDay": 5,
  "countryId": 91,
  "timeZone": "Asia/Kolkata",
  "createdBy": 9
}
```

> This used to be `userId`, `countryId` and `createdBy` alone. Everything else
> became mandatory on 2026-09-22 — a setup missing its hours, its week or its
> contract is one nothing downstream can compute a week from, and it was being
> accepted and stored.

#### Success response — `200 OK`

`data` is an `AdminResponse` — the **administrative** view, carrying the
whole row including the audit columns, because an admin screen has to show who
last changed a setup.

| Field | Type | Meaning |
|---|---|---|
| `setupId` | int | The row's key |
| `userId` | int? | Who the setup belongs to |
| `maxTimeInHrs` | string? | The `MaxTimeinhrs` column, `hh:mm:ss` |
| `maxTimInMins` | string? | The `MaxTiminmins` column, `hh:mm:ss` |
| `organizationId` | int? | |
| `contractType` | int? | |
| `startDay` | int? | A `dbo.DayMaster.DayID`, 1 = Monday … 7 = Sunday |
| `endDay` | int? | A `dbo.DayMaster.DayID` |
| `exceptionDay` | int? | A `dbo.DayMaster.DayID` — a day worked in addition to the normal week |
| `countryId` | int? | Stored as sent |
| `countryName` | string? | **Derived** — the country's name from `dbo.Country`. `null` when the setup names no country, **or names one that does not exist** |
| `timeZone` | string? | The IANA zone id **exactly as it was sent** — not resolved, and not necessarily one the country has |
| `countryWithTimeZone` | string? | **Derived** — `countryName` and `timeZone` joined with a hyphen, `"India-Asia/Kolkata"`. The same string [Country Time Zone List](#9-country-time-zone-list) returns as `optionValue` for that pairing |
| `timeEntryLockAt` | string? | `hh:mm:ss`, measured in `timeZone` |
| `needToSendReminder` | bool? | Whether the user is sent a reminder. `null` means no |
| `reminderTimeBeforeCutoff` | string? | `hh:mm:ss` — how long before `timeEntryLockAt` the reminder goes out |
| `createdBy` | int? | audit |
| `createDate` | datetime? | audit |
| `updatedBy` | int? | audit |
| `updateDate` | datetime? | audit |

#### Example response

```json
{
  "success": true,
  "message": "Timesheet setup saved.",
  "data": {
    "setupId": 17,
    "userId": 101,
    "maxTimeInHrs": "08:00:00",
    "maxTimInMins": "00:30:00",
    "organizationId": 3,
    "contractType": 1,
    "startDay": 1,
    "endDay": 5,
    "exceptionDay": 7,
    "countryId": 91,
    "countryName": "India",
    "timeZone": "Asia/Kolkata",
    "countryWithTimeZone": "India-Asia/Kolkata",
    "timeEntryLockAt": "18:00:00",
    "needToSendReminder": true,
    "reminderTimeBeforeCutoff": "00:30:00",
    "createdBy": 9,
    "createDate": "2026-09-01T08:15:02.443",
    "updatedBy": 9,
    "updateDate": "2026-09-17T10:22:41.9801234Z"
  },
  "errors": []
}
```

On a fresh insert, `updatedBy` and `updateDate` come back as `null` — present in
the payload, with no value yet.

The message is deliberately `"Timesheet setup saved."` rather than "added" or
"updated" — the controller no longer knows which happened, and the saved row is
in the response if the client cares.

#### Error responses

**400 — shape validation:**

| Trigger | Message |
|---|---|
| `userId <= 0` | `UserId is required: a setup must belong to a user.` |
| `createdBy <= 0` | `CreatedBy is required: the row records who created or changed it.` |
| `organizationId` missing | `OrganizationId is required.` |
| `organizationId <= 0` | `OrganizationId must be greater than 0.` |
| `contractType` missing | `ContractType is required.` |
| `contractType` not 1 or 2 | `ContractType must be 1 (Full Time) or 2 (Part Time).` |
| `countryId` missing | `CountryId is required.` |
| `countryId <= 0` | `CountryId must be greater than 0.` |
| `timeZone` missing, `""` or whitespace | `TimeZone is required.` |
| `timeZone` longer than 100 characters | `TimeZone must be 100 characters or fewer.` |
| `startDay` missing | `StartDay is required.` |
| `startDay` out of range | `StartDay must be a DayMaster day id between 1 (Monday) and 7 (Sunday).` |
| `endDay` missing | `EndDay is required.` |
| `endDay` out of range | `EndDay must be a DayMaster day id between 1 (Monday) and 7 (Sunday).` |
| `exceptionDay` out of range | `ExceptionDay must be a DayMaster day id between 1 (Monday) and 7 (Sunday).` |

**400 — the time fields.** These are read by `AdminService` rather than the
validator, for the same reason as on the time-log write, and are reported **one
at a time** — the service stops at the first thing it cannot accept.

`maxTimeInHrs` and `maxTimInMins` are now **required**, so an absent one is an
error; `timeEntryLockAt` and `reminderTimeBeforeCutoff` are still optional, and
absent is simply `null`.

| Trigger | Message |
|---|---|
| `maxTimeInHrs` missing | `MaxTimeInHrs is required, as a time of day in hh:mm:ss format - for example "09:00:00".` |
| `maxTimeInHrs` malformed, or outside a day | `MaxTimeInHrs must be a time of day in hh:mm:ss format, between "00:00:00" and "23:59:59" - for example "09:00:00".` |
| `maxTimeInHrs` above `23:00:00` | `MaxTimeInHrs must be between "00:00:00" and "23:00:00".` |
| `maxTimInMins` missing | `MaxTimInMins is required, as a time of day in hh:mm:ss format - for example "09:00:00".` |
| `maxTimInMins` malformed, or outside a day | `MaxTimInMins must be a time of day in hh:mm:ss format, between "00:00:00" and "23:59:59" - for example "09:00:00".` |
| `maxTimInMins` above `00:59:00` | `MaxTimInMins must be between "00:00:00" and "00:59:00".` |
| `timeEntryLockAt` sent and malformed | `TimeEntryLockAt must be a time of day in hh:mm:ss format, between "00:00:00" and "23:59:59" - for example "09:00:00".` |
| `reminderTimeBeforeCutoff` sent and malformed | `ReminderTimeBeforeCutoff must be a time of day in hh:mm:ss format, between "00:00:00" and "23:59:59" - for example "09:00:00".` |

> Two messages per bounded field, deliberately. `"25:00:00"` is not a time of
> day at all and gets the format message; `"23:30:00"` is a perfectly good time
> that this particular field does not accept, and gets the range one. Telling a
> caller their valid time is malformed would send them looking for a typo.

```json
{
  "success": false,
  "message": "One or more validation errors occurred.",
  "data": null,
  "errors": [
    "UserId: UserId is required: a setup must belong to a user.",
    "EndDay: EndDay is required.",
    "TimeZone: TimeZone is required."
  ]
}
```

**500** — unhandled defect.

> **No 404, and no country-related 400.** Both were removed on 2026-09-22 with
> the time-zone resolution. Nothing on this path reads `dbo.Country`, so there
> is no country to fail to find and no zone list to fail against. Every failure
> this endpoint can now produce is a **400**: a shape failure from the validator,
> or a time-field failure from `AdminService`.
>
> The save also never fails to find the **setup**: a user who has none gets one
> created.

---

### 6. Admin Timesheet Setup

`GET /api/v1/Admin/get-user-timesheet-setup/{userID}`

**Purpose** — Return the timesheet setup belonging to one user, in the
administrative shape (whole row, audit columns included). A user has at most one,
so this is a single object rather than a list.

Compare with [Employee Timesheet Setup](#2-employee-timesheet-setup),
which returns the nine-column timesheet-screen projection for the same user.

#### Route parameters

| Parameter | Type | Constraint |
|---|---|---|
| `userID` | int | `:int:min(1)` — below 1 matches no route and returns **404** |

> Spelled `userID`, matching the route token character for character. Swagger UI
> substitutes path parameters **case-sensitively**; a parameter named `userId`
> against a `{userID}` token would send the literal text `{userID}`, fail the
> `:int` constraint and match no route — a confusing **404**.

No request body.

#### Example request

```http
GET /api/v1/Admin/get-user-timesheet-setup/101
```

#### Success response — `200 OK`

`data` is an `AdminResponse` — same shape as
[Save Timesheet Setup](#5-save-timesheet-setup), including the two
**derived** fields `countryName` and `countryWithTimeZone`.

```json
{
  "success": true,
  "message": "",
  "data": {
    "setupId": 17,
    "userId": 101,
    "maxTimeInHrs": "08:00:00",
    "maxTimInMins": "00:30:00",
    "organizationId": 3,
    "contractType": 1,
    "startDay": 1,
    "endDay": 5,
    "exceptionDay": 7,
    "countryId": 91,
    "countryName": "India",
    "timeZone": "Asia/Kolkata",
    "countryWithTimeZone": "India-Asia/Kolkata",
    "timeEntryLockAt": "18:00:00",
    "needToSendReminder": true,
    "reminderTimeBeforeCutoff": "00:30:00",
    "createdBy": 9,
    "createDate": "2026-09-01T08:15:02.443",
    "updatedBy": null,
    "updateDate": null
  },
  "errors": []
}
```

#### The country, three ways

| Field | Source |
|---|---|
| `countryId` | **Stored** — the `CountryID` column, exactly as the save was given it |
| `countryName` | **Derived** — the country's name, looked up in `dbo.Country` |
| `countryWithTimeZone` | **Derived** — `countryName` and `timeZone` joined with a hyphen, `"India-Asia/Kolkata"` |

Neither derived field is stored; both are built on the way out from one lookup
of `countryId`.

`countryWithTimeZone` holds **exactly** the string
[Country Time Zone List](#9-country-time-zone-list) returns as
`optionValue` for the same pairing — the two are built by one shared joiner. So:
load the picker from **Country Time Zone List**, load the setup from here, and preselect the
entry whose `optionValue` equals this. One string comparison, no reassembly.

> The names differ, the values do not. **Country Time Zone List** calls it `optionValue`
> because it is a dropdown option; here it is `countryWithTimeZone` because it
> describes the setup. Compare the values, never the key names.

Only the parts that exist are joined, so there is never a dangling hyphen:

| The setup holds | `countryName` | `countryWithTimeZone` |
|---|---|---|
| A real country and a zone | `"India"` | `"India-Asia/Kolkata"` |
| A country id **no country has** | `null` | `"Asia/Kolkata"` — the bare zone |
| A country but no zone | `"India"` | `"India"` |
| Neither | `null` | `null` |

> The second row is reachable: [Save Timesheet Setup](#5-save-timesheet-setup)
> stores `countryId` without checking it exists. A null `countryName` beside a
> non-null `countryId` is the signal that the stored id has no matching row.

#### When the user has no setup

**`200`, `success: true`, `data: null`** — not a 404, and that includes a setup
that was **soft-deleted**, which the global query filter hides.

```json
{
  "success": true,
  "message": "This user has no timesheet setup.",
  "data": null,
  "errors": []
}
```

Finding an employee who has not been set up is one of the reasons to call this,
so it is an answer rather than an error. Follow it with
[Save Timesheet Setup](#5-save-timesheet-setup), which needs no
insert/update distinction — send the same payload either way.

#### Error responses

| Status | Cause |
|---|---|
| **404** | `userID` below 1 — no route matches. This is routing, not "no data" |
| **500** | Unhandled defect |

---

### 7. Delete Timesheet Setup

`POST /api/v1/Admin/delete-timesheet-setup`

**Purpose** — Soft-delete a timesheet setup. The row is **never** removed: it is
marked `IsDelete = 1` and stamped with who deleted it and when, so the history
survives and a global query filter simply stops returning it.

**It also closes the user's unsent emails.** Every `dbo.EmailQueue` row for the
setup's user that is still `Pending` (1) or `Error` (3) is set to
`EmailStatusID = 2` (Sent) with `ErrorMessage = 'Setup deleted'`, so the sender
skips it. `SentDate` stays `null` — nothing was sent — which is what tells these
rows apart from real sends. A `Processing` (4) email is mid-send and is left
alone. The delete and the closed emails are written in **one transaction**: both
happen, or neither.

A POST rather than an HTTP DELETE because the request carries a body —
`deletedBy` has to travel with it — and a body on DELETE is inconsistently
supported by proxies and HTTP clients. It also matches the rest of this API,
where every argument travels in the payload.

#### Request body — `AdminDeleteRequest`

| Field | Type | Required | Rules | Notes |
|---|---|---|---|---|
| `setupId` | int | yes | `> 0` | Must identify a **live**, non-deleted row |
| `deletedBy` | int | yes | `> 0` | Written to the `Deletedby` column. **Temporary** — comes from the token once auth is on |

Note this endpoint takes a **`setupId`**, not a `userId` — unlike the save
endpoint. Read the setup first if you only have the user.

#### Example request

```json
{
  "setupId": 17,
  "deletedBy": 9
}
```

#### Success response — `200 OK`

`data` is `null` — there is nothing meaningful to return once the row is hidden.

```json
{
  "success": true,
  "message": "Timesheet setup deleted.",
  "data": null,
  "errors": []
}
```

#### Error responses

**400 — shape validation:**

| Trigger | Message |
|---|---|
| `setupId <= 0` | `SetupId is required and must be greater than 0.` |
| `deletedBy <= 0` | `DeletedBy is required: a soft delete records who performed it.` |

```json
{
  "success": false,
  "message": "One or more validation errors occurred.",
  "data": null,
  "errors": [
    "SetupId: SetupId is required and must be greater than 0.",
    "DeletedBy: DeletedBy is required: a soft delete records who performed it."
  ]
}
```

**404 — no live setup has that id.** Deleting one twice is **not** a silent
success:

```json
{
  "success": false,
  "message": "Timesheet setup '17' was not found.",
  "data": null,
  "errors": []
}
```

**500** — unhandled defect.

> A deleted setup is not gone for good: saving that user's setup again
> ([Save Timesheet Setup](#5-save-timesheet-setup)) revives the
> same row and clears the delete stamps. **The emails it closed stay closed** —
> reviving a setup does not reopen them.

---

### 8. Employee List

`GET /api/v1/Admin/get-all-employees-by-orgid/{orgID}`

**Purpose** — Every employee in one organisation, with their contracted time per
week, what they have logged in the **current Monday–Sunday week**, progress
against the contract, and head-count totals for the same rows.

Executes `dbo.spc_GetEmployeeListByPOrgID`. Every figure in a row is computed by
the procedure and passed straight through; the API adds only the `summary`.

#### Request

| Parameter | In | Type | Required | Rules |
|---|---|---|---|---|
| `orgID` | route | int | yes | `> 0`. Omitted, the URL matches no route and is a `404` |

```
GET /api/v1/Admin/get-all-employees-by-orgid/700
```

> **This is everyone in the organisation, not just employees.** Until
> 2026-09-22 the procedure filtered `dbo.Signup` on `RoleID = 2`; that predicate
> is now commented out, so administrators and managers appear in the grid and
> in its head-count summary alongside employees. The only filters left are the
> organisation and `isdelete = 0`, which excludes soft-deleted people.
>
> The endpoint's route and field names still say "employee" — they predate the
> change. If the role filter comes back, note for then: `RoleID = 2` meant
> "employee" to the procedure, which does **not** line up with
> [`RoleType`](#roletype), where `2` is `Manager`. The two vocabularies
> genuinely differ and nothing reconciles them.

#### Success response — `200 OK`

`data` is an `EmployeeListResponse`: `summary` first, then `employees`.

**`summary`** — counted from the rows in the same response, never queried
separately, so the totals can never disagree with the grid beneath them.

| Field | Type | Meaning |
|---|---|---|
| `totalEmployees` | int | Rows returned |
| `totalEmployeesWithSetup` | int | How many have a `dbo.TimesheetMasterSetup` row |
| `totalEmployeesWithoutSetup` | int | How many have none. With the previous field, always adds up to `totalEmployees` |
| `totalFullTime` | int | `contractTypeId == 1` |
| `totalPartTime` | int | `contractTypeId == 2` |

> **Full time + part time need not equal the head count.** An employee with no
> setup has no contract, and a setup can carry a contract id that is neither 1
> nor 2. Those rows count towards neither — inventing a default would report a
> contract nobody chose.

**`employees[]`**

| Field | Type | Meaning |
|---|---|---|
| `userId` | int | `dbo.Signup.UserID` |
| `setupId` | int? | Their setup's key, or `null` when they have none. **A soft-deleted setup is still returned here** — the procedure does not filter `IsDelete` — so a non-null `setupId` means "has a row", not "has a live setup". The expected-time fields are also null for a setup that exists but is half-filled |
| `name` | string? | From `dbo.Signup` |
| `email` | string? | From `dbo.Signup` |
| `expectedHoursPerWeek` | int? | Whole contracted hours per week. Null when there is no setup, it is incomplete, or its week wraps past Sunday (`endDay < startDay`) |
| `expectedMinsPerWeek` | int? | The **remainder** that goes with it, not a separate quantity: 37.5 h/week is `37` and `30` |
| `expectedHoursPerWeekText` | string? | The same figure formatted by the procedure — `"37h 30m/week"` |
| `totalLoggedHoursCurrentWeek` | int | Whole hours logged this week. `0` rather than null when nothing was logged |
| `totalLoggedMinsCurrentWeek` | int | The remainder that goes with it |
| `totalLoggedHoursCurrentWeekText` | string? | `"12h 45m"` |
| `progressOnThisWeek` | decimal | Percent of the contracted week logged, **already capped at 100 by the procedure** so a client can draw a bar without clamping it again. `0` when there is nothing to measure against |
| `contractTypeId` | int? | `1` = full time, `2` = part time |
| `contractType` | string? | `"Full Time"` / `"Part Time"`. Null for any other id |
| `countryId` | int? | The employee's country, from **`dbo.Signup.CountryID`**. Added 2026-09-21 |

> `countryId` is the one nullable field here that says nothing about the
> timesheet setup. Every other null above means "this employee has no setup, or
> a half-filled one"; `countryId` comes from `dbo.Signup`, the side of the
> `LEFT JOIN` that always exists, so a null means the **signup** names no
> country. It is also not the same column as the `countryId` on
> [Save Timesheet Setup](#5-save-timesheet-setup)'s response, which is
> `dbo.TimesheetMasterSetup.CountryID` — the two can hold different values.

#### Example response

```json
{
  "success": true,
  "message": "",
  "data": {
    "summary": {
      "totalEmployees": 3,
      "totalEmployeesWithSetup": 2,
      "totalEmployeesWithoutSetup": 1,
      "totalFullTime": 1,
      "totalPartTime": 1
    },
    "employees": [
      {
        "userId": 5001,
        "setupId": 17,
        "name": "Priya Raman",
        "email": "priya@etimesheet.test",
        "expectedHoursPerWeek": 40,
        "expectedMinsPerWeek": 0,
        "expectedHoursPerWeekText": "40h/week",
        "totalLoggedHoursCurrentWeek": 8,
        "totalLoggedMinsCurrentWeek": 0,
        "totalLoggedHoursCurrentWeekText": "8h",
        "progressOnThisWeek": 20,
        "contractTypeId": 1,
        "contractType": "Full Time",
        "countryId": 91
      },
      {
        "userId": 5003,
        "setupId": null,
        "name": "Sam Patel",
        "email": "sam@etimesheet.test",
        "expectedHoursPerWeek": null,
        "expectedMinsPerWeek": null,
        "expectedHoursPerWeekText": null,
        "totalLoggedHoursCurrentWeek": 0,
        "totalLoggedMinsCurrentWeek": 0,
        "totalLoggedHoursCurrentWeekText": "0h",
        "progressOnThisWeek": 0,
        "contractTypeId": null,
        "contractType": null,
        "countryId": 91
      }
    ]
  },
  "errors": []
}
```

The second employee has no setup, so `setupId`, every expected-time field and
both contract fields come back as `null`. **They are still there.** Both rows
carry the same fourteen keys, which is what lets a grid bind to the response
without a per-row existence check.

Note that the second employee still has a `countryId`: it comes from their
signup, which exists whether or not anybody has given them a timesheet setup.

#### Error responses

**400 — the organisation id is not positive** (`errors[]` populated). The
segment is constrained to `:int` but deliberately not `:min(1)`, so a `0`
reaches the service and is told what is wrong with it:

```json
{
  "success": false,
  "message": "One or more validation errors occurred.",
  "data": null,
  "errors": [
    "orgID: orgID is required and must be greater than 0."
  ]
}
```

> **An organisation with nobody in it is `200` with an empty grid, not a 404.**
> "This organisation has no employees" is an answer. Nothing here can tell an
> empty organisation apart from one that does not exist — the procedure returns
> no rows either way — so inventing a 404 would be a guess.

#### Things worth knowing

- **The week is the server's.** The procedure derives Monday–Sunday from
  `GETDATE()` inside SQL Server. It is not affected by any clock the API
  injects, and an integration test has to arrange its rows against the
  database's own answer rather than the test host's.
- **An entry spanning the week boundary is clipped**, not counted whole: the
  procedure trims it to the part that falls inside the week.
- **Deleted time logs are not excluded.** The procedure does not filter
  `dbo.TimeLog.IsDeleted`, so a soft-deleted entry still counts towards the
  logged total — unlike every other read in this API.
- **An employee holding two setup rows would appear twice**, and be counted
  twice. The Admin save path makes that impossible, but no database constraint
  does.
- **Soft-deleted setups are not excluded.** See `setupId` above; the Admin
  Timesheet Setup read and the dashboards both hide them.
- **The figures will not match Admin Dashboard Summary.** This list uses a fixed
  Monday–Sunday week, counts deleted time logs and deleted setups, and treats a
  wrapping week as no contract; the dashboard uses each employee's own week and
  excludes all three.

---

### 9. Country Time Zone List

`GET /api/v1/Admin/get-country-list-with-timezones`

**Purpose** — Return every country paired with each of its time zones: the flat
list a setup screen's country picker binds to.

**One entry per time zone, not one per country.** `dbo.Country.TimeZone` holds a
country's IANA zones comma-separated, and this unpacks the whole lookup — the
United Kingdom is one entry, the United States is twenty-nine, and **all of them
repeat the same `countryId`**. `countryId` is therefore *not* unique in the
list; the country and the zone together identify an entry.

Each entry carries the parts **and** the joined label, so a client displays
`optionValue` and sends `countryId` + `timeZone` straight back to the save,
without ever splitting a string.

> Replaces the old `get-country-timezones-by-countryid/{countryID}`, which took
> a country id and returned bare zone strings. That could only be called *after*
> a country had been chosen, which is backwards — the client needs the list in
> order to build the choice. One call at screen load now, instead of one per
> country the user clicks.

#### Parameters

None. No route parameter, no query string, no body.

#### Example request

```http
GET /api/v1/Admin/get-country-list-with-timezones
```

#### Success response — `200 OK`

`data` is a **list of objects**:

| Field | Type | Meaning |
|---|---|---|
| `countryId` | int | The `dbo.Country.ID` — send this back as `countryId` on [Save Timesheet Setup](#5-save-timesheet-setup). Repeated across every entry of a multi-zone country |
| `countryName` | string | The country's name on its own — `"United States"`. `""` for the rare row whose `Name` column is null |
| `timeZone` | string | The IANA zone id on its own — `"America/New_York"`. Send this back as `timeZone` on **Save Timesheet Setup** |
| `optionValue` | string | The two joined with a hyphen — `"United States-America/New_York"`. The label to display and to key a selection on |

```json
{
  "success": true,
  "message": "",
  "data": [
    {
      "countryId": 13,
      "countryName": "Australia",
      "timeZone": "Australia/Sydney",
      "optionValue": "Australia-Australia/Sydney"
    },
    {
      "countryId": 13,
      "countryName": "Australia",
      "timeZone": "Australia/Perth",
      "optionValue": "Australia-Australia/Perth"
    },
    {
      "countryId": 101,
      "countryName": "India",
      "timeZone": "Asia/Kolkata",
      "optionValue": "India-Asia/Kolkata"
    },
    {
      "countryId": 232,
      "countryName": "United Kingdom",
      "timeZone": "Europe/London",
      "optionValue": "United Kingdom-Europe/London"
    },
    {
      "countryId": 233,
      "countryName": "United States",
      "timeZone": "America/New_York",
      "optionValue": "United States-America/New_York"
    },
    {
      "countryId": 233,
      "countryName": "United States",
      "timeZone": "America/Chicago",
      "optionValue": "United States-America/Chicago"
    }
  ],
  "errors": []
}
```

#### Order

Countries by **name**, sorted by SQL Server; within a country, its zones in the
order the column lists them — so a country's **first** entry is its primary zone
and the sensible one to preselect.

#### What is not in the list

A country whose `TimeZone` column has never been filled in contributes **no
entries at all**, so a picker built from this list cannot offer it. The save
would not stop you — it stores `countryId` and `timeZone` unchecked — but a
setup with no usable zone falls back to UTC. If a country is missing from this
list, its `dbo.Country` row needs a time zone.

#### How the client uses it

Bind the dropdown's **text** to `optionValue` and its **value** to the entry's
`countryId` + `timeZone`. Send the selected entry's `countryId` and `timeZone`
verbatim to `save-user-timesheet-setup`. Both are **required** there and
**stored exactly as sent** — the save does not read `dbo.Country`, so nothing
checks that the pair belongs together. This list is the only thing keeping them
consistent.

> Never parse `optionValue` to get the zone back — `timeZone` is right there in
> the same entry, in the country's own spelling. Sending that spelling is what
> lets Admin Timesheet Setup's `countryWithTimeZone` match this entry's
> `optionValue` later.

#### Empty result

Only if `dbo.Country` holds no time zones whatsoever — `200` with an empty list.

#### Error responses

| Status | Cause |
|---|---|
| **500** | Unhandled defect |

There is no 400 and no 404: the endpoint takes no input, so there is nothing to
reject and nothing to fail to find.

---

### 11. Admin Dashboard Summary

`GET /api/v1/Admin/get-admin-dashboard-summary-by-orgID/{orgID}`

**Purpose** — One organisation's dashboard figures: head counts, and this week's
logged and expected time across all its employees.

Backed by `spc_GetAdminDashboardSummaryByOrgID`. The route is matched without
regard to case.

> **Not yet live.** `dbo.spc_GetAdminDashboardSummaryByOrgID` is recorded as a
> **DRAFT** and has not been applied to any database. Until the owner creates
> it, every call fails with **500**.

#### Route parameters

| Parameter | Type | Constraint |
|---|---|---|
| `orgID` | int | `:int` only — `0` or below reaches the service and is a **400** |

No request body.

#### How the figures are worked out

- **Employees** are every non-deleted `dbo.Signup` row in the organisation,
  whatever their role — as on [Employee List](#8-employee-list).
- **Full time / part time** count employees with a live setup whose contract
  type is 1 / 2. An employee with no setup is in neither, so the two need not add
  up to `totalEmployees`.
- **This week** is each employee's **own** timesheet week (their setup's
  `startDay`, Monday when none), so the totals equal the sum of each employee's
  [User Dashboard Summary](#10-user-dashboard-summary). "Today" is the database
  server's date.
- **Logged** counts every live entry, `Save` and `Draft`, by every employee —
  with or without a setup.
- **Expected** sums each employee's working days (`startDay`..`endDay`, wrapping
  past Sunday; `exceptionDay` adds nothing) × daily time; an employee with no
  (or an incomplete) setup adds 0.
- **`pendingTimesheets` and `approvedTimesheets` are static placeholders** —
  always `0` until an approval workflow exists.

#### Success response — `200 OK`

`data` is an `AdminDashboardSummaryResponse`:

| Field | Type | Meaning |
|---|---|---|
| `organizationId` | int | The organisation |
| `totalEmployees` | int | Non-deleted people in the organisation |
| `totalFullTimeEmployees` | int | With a live Full Time setup |
| `totalPartTimeEmployees` | int | With a live Part Time setup |
| `weekLoggedMinutes` | int | Minutes logged this week, all employees |
| `weekLoggedText` | string? | The same, as text — `"327h 30m"` |
| `weekExpectedMinutes` | int | Minutes expected this week, all employees |
| `weekExpectedText` | string? | The same, as text |
| `pendingTimesheets` | int | **Static** — `0` |
| `approvedTimesheets` | int | **Static** — `0` |

#### Example request

```http
GET /api/v1/Admin/get-admin-dashboard-summary-by-orgID/12
```

#### Example response

```json
{
  "success": true,
  "message": "",
  "data": {
    "organizationId": 12,
    "totalEmployees": 24,
    "totalFullTimeEmployees": 18,
    "totalPartTimeEmployees": 4,
    "weekLoggedMinutes": 19650,
    "weekLoggedText": "327h 30m",
    "weekExpectedMinutes": 50400,
    "weekExpectedText": "840h",
    "pendingTimesheets": 0,
    "approvedTimesheets": 0
  },
  "errors": []
}
```

An organisation with nobody in it is **`200`** with every figure `0` — never a
404.

#### Error responses

| Status | Cause | Example message |
|---|---|---|
| **400** | `orgID` is `0` or below | `message`: `One or more validation errors occurred.`; `errors`: `["orgID: orgID is required and must be greater than 0."]` |
| **500** | Unhandled defect — including, for now, the procedure not existing yet | — |

---

### 16. User Role List

`GET /api/v1/Admin/get-user-role-list-by-orgid/{orgID}`

**Purpose** — The roles defined for one organisation, from
`dbo.TimesheetUserRole`, for a role picker. Read through
`dbo.spc_GetUserRoleListByOrgID`.

Only **live** roles come back: `IsDelete` is a *nullable* bit, and a role is
live when it is `0` **or** `NULL` — only `1` means deleted. A role with no
`OrganizationID` belongs to nobody and never appears.

#### Route parameters

| Name | Type | Rules |
|---|---|---|
| `orgID` | int | `> 0` — checked by the service, so a `0` is a 400 naming the field rather than a missed route |

#### Example request

```
GET /api/v1/Admin/get-user-role-list-by-orgid/12
```

#### Success response — `200 OK`

`data` is an array of `UserRoleResponse`, ordered by `roleName`, then `roleId`:

| Field | Type | Notes |
|---|---|---|
| `roleId` | int | `dbo.TimesheetUserRole.RoleID` |
| `roleName` | string \| null | The `Rolename` column; nullable on the table |

`organizationId` is **not** returned — the caller already sent it in the URL.

#### Example response

```json
{
  "success": true,
  "message": "",
  "data": [
    { "roleId": 4, "roleName": "Developer" },
    { "roleId": 2, "roleName": "Project Manager" }
  ],
  "errors": []
}
```

#### Empty result

An organisation with no roles is `data: []` — a 200, not a 404.

#### Error responses

**400** — `orgID <= 0`:

```json
{
  "success": false,
  "message": "One or more validation errors occurred.",
  "data": null,
  "errors": ["orgID: orgID is required and must be greater than 0."]
}
```

**500** — unhandled defect.

---

### 17. Add / Edit User Role

`POST /api/v1/Admin/add-edit-user-role`

**Purpose** — Add a role to an organisation, or rename one. **One endpoint for
both**, chosen by `roleId`. Written through `dbo.spc_AddEditUserRoleByAdmin`.

| `roleId` sent | Result |
|---|---|
| absent or `0` | **Adds** a live role to `organizationId`. `RoleID` is `IDENTITY` — the database assigns it, and it comes back in the response |
| `> 0`, a live role of `organizationId` | **Renames** it. `Createdby` and `CreateDate` keep their original values |
| `> 0`, but no such role, deleted, or another organisation's | **404** — never edited |

#### Request body — `UserRoleSaveRequest` (JSON)

| Field | Type | Required | Rules | Notes |
|---|---|---|---|---|
| `roleId` | int \| null | no | `>= 0` | absent / `0` adds; an id edits |
| `organizationId` | int | yes | `> 0` | On edit, the role must belong to it |
| `roleName` | string | yes | not blank, `<= 50` characters **after trimming** | Unique among the organisation's live roles, **ignoring case** |
| `createdBy` | int | yes | `> 0` | Written to `Createdby` on add only. **Temporary** — comes from the token once auth is on |

Validation runs in **`AdminService`**, not a FluentValidation validator — but the
400 is the same shape, and lists **every** failing field at once.

`CreateDate` is the API's clock (UTC), passed to the procedure.

#### Example request — add

```json
{
  "roleId": 0,
  "organizationId": 12,
  "roleName": "Project Manager",
  "createdBy": 5
}
```

#### Example request — rename

```json
{
  "roleId": 2,
  "organizationId": 12,
  "roleName": "Senior Project Manager",
  "createdBy": 5
}
```

#### Success response — `200 OK`

`data` is a `UserRoleResponse` — the saved role, the same shape as
[User Role List](#16-user-role-list):

```json
{
  "success": true,
  "message": "User role saved.",
  "data": { "roleId": 7, "roleName": "Project Manager" },
  "errors": []
}
```

#### Error responses

**400 — validation** (every failure listed):

| Trigger | Message |
|---|---|
| `roleId < 0` | `RoleId must be 0 (or left out) to add a role, or the id of the role to edit.` |
| `organizationId <= 0` | `OrganizationId is required and must be greater than 0.` |
| `roleName` blank | `RoleName is required.` |
| `roleName` longer than 50 | `RoleName must be 50 characters or fewer.` |
| `createdBy <= 0` | `CreatedBy is required: the row records who created it.` |

```json
{
  "success": false,
  "message": "One or more validation errors occurred.",
  "data": null,
  "errors": [
    "OrganizationId: OrganizationId is required and must be greater than 0.",
    "RoleName: RoleName is required."
  ]
}
```

**404** — `roleId > 0` names no live role of `organizationId`:

```json
{ "success": false, "message": "Role '7' was not found.", "data": null, "errors": [] }
```

**409** — the organisation already has **another** live role of that name
(case-insensitive). Saving a role under its own name is fine, and a deleted
role's name is free to reuse:

```json
{
  "success": false,
  "message": "Organization '12' already has a role named 'Project Manager'.",
  "data": null,
  "errors": []
}
```

**500** — unhandled defect.

---

### 18. Delete User Role

`POST /api/v1/Admin/delete-user-role`

**Purpose** — Soft-delete a role of an organisation. The row is **never**
removed: it is marked `IsDelete = 1` and stamped with `Deleteddate` (UTC) and
`Deletedby`. From then on the role list and Add / Edit User Role no longer see
it. Done by the API through EF Core — no stored procedure.

A POST rather than an HTTP DELETE, as with
[Delete Timesheet Setup](#7-delete-timesheet-setup).

#### Request body — `UserRoleDeleteRequest` (JSON)

| Field | Type | Required | Rules | Notes |
|---|---|---|---|---|
| `roleId` | int | yes | `> 0` | |
| `organizationId` | int | yes | `> 0` | The role must belong to it — another organisation's role is a 404, never deleted |
| `deletedBy` | int | yes | `> 0` | Written to `Deletedby`. **Temporary** — comes from the token once auth is on |

Validated in **`AdminService`**; every failing field is listed at once.

#### Example request

```json
{ "roleId": 7, "organizationId": 12, "deletedBy": 5 }
```

#### Success response — `200 OK`

```json
{ "success": true, "message": "User role deleted.", "data": null, "errors": [] }
```

#### Error responses

**400 — validation:**

| Trigger | Message |
|---|---|
| `roleId <= 0` | `RoleId is required and must be greater than 0.` |
| `organizationId <= 0` | `OrganizationId is required and must be greater than 0.` |
| `deletedBy <= 0` | `DeletedBy is required: a soft delete records who performed it.` |

**404** — no live role with that id in that organisation. Deleting one twice is
**not** a silent success, and the first `Deleteddate` is never overwritten:

```json
{
  "success": false,
  "message": "Role '7' was not found in organization '12'.",
  "data": null,
  "errors": []
}
```

**500** — unhandled defect.

> Users still assigned to the role do **not** stop the delete — see
> [Known gaps](#known-gaps).

---

### 22. Project Category List

`GET /api/v1/Admin/get-timesheet-project-category-by-orgID/{orgID}`

**Purpose** — The project categories defined for one organisation, from
`dbo.TimesheetProjectCategory`, for a category picker. Read through
`dbo.spc_GetTimesheetProjectCategory`.

Only **live** categories come back: `IsDelete` is a *nullable* bit, and a
category is live when it is `0` **or** `NULL` — only `1` means deleted.

#### Route parameters

| Name | Type | Rules |
|---|---|---|
| `orgID` | int | `> 0` — checked by the service, so a `0` is a 400 naming the field rather than a missed route |

#### Example request

```
GET /api/v1/Admin/get-timesheet-project-category-by-orgID/12
```

#### Success response — `200 OK`

`data` is an array of `ProjectCategoryResponse`, ordered by `categoryName`,
then `categoryId`:

| Field | Type | Notes |
|---|---|---|
| `categoryId` | int | `dbo.TimesheetProjectCategory.ID` |
| `categoryName` | string \| null | The `Categoryname` column; nullable on the table |

`organizationId` is **not** returned — the caller already sent it in the URL.

#### Example response

```json
{
  "success": true,
  "message": "",
  "data": [
    { "categoryId": 3, "categoryName": "Development" },
    { "categoryId": 1, "categoryName": "Testing" }
  ],
  "errors": []
}
```

#### Empty result

An organisation with no categories is `data: []` — a 200, not a 404.

#### Error responses

**400** — `orgID <= 0`:

```json
{
  "success": false,
  "message": "One or more validation errors occurred.",
  "data": null,
  "errors": ["orgID: orgID is required and must be greater than 0."]
}
```

**500** — unhandled defect, or the procedure has not been created in the
database yet.

---

### 23. Add / Edit Project Category

`POST /api/v1/Admin/add-edit-timesheet-project-category`

**Purpose** — Add a project category to an organisation, or rename one. **One
endpoint for both**, chosen by `categoryId`. Written through
`dbo.spc_AddEditTimesheetProjectCategory`.

| `categoryId` sent | Result |
|---|---|
| absent or `0` | **Adds** a live category to `organizationId`. `ID` is `IDENTITY` — the database assigns it, and it comes back in the response |
| `> 0`, a live category of `organizationId` | **Renames** it. `CreatedBy` and `CreateDate` keep their original values |
| `> 0`, but no such category, deleted, or another organisation's | **404** — never edited |

#### Request body — `ProjectCategorySaveRequest` (JSON)

| Field | Type | Required | Rules | Notes |
|---|---|---|---|---|
| `categoryId` | int \| null | no | `>= 0` | absent / `0` adds; an id edits |
| `organizationId` | int | yes | `> 0` | On edit, the category must belong to it |
| `categoryName` | string | yes | not blank, `<= 50` characters **after trimming** | **Not** checked for uniqueness — see [Known gaps](#known-gaps) |
| `createdBy` | int | yes | `> 0` | Written to `CreatedBy` on add only. **Temporary** — comes from the token once auth is on |

Validation runs in **`AdminService`**, not a FluentValidation validator — but the
400 is the same shape, and lists **every** failing field at once.

`CreateDate` is the API's clock (UTC), passed to the procedure.

#### Example request — add

```json
{
  "categoryId": 0,
  "organizationId": 12,
  "categoryName": "Development",
  "createdBy": 5
}
```

#### Example request — rename

```json
{
  "categoryId": 3,
  "organizationId": 12,
  "categoryName": "Software Development",
  "createdBy": 5
}
```

#### Success response — `200 OK`

`data` is a `ProjectCategoryResponse` — the saved category, the same shape as
[Project Category List](#22-project-category-list):

```json
{
  "success": true,
  "message": "Project category saved.",
  "data": { "categoryId": 7, "categoryName": "Development" },
  "errors": []
}
```

#### Error responses

**400 — validation** (every failure listed):

| Trigger | Message |
|---|---|
| `categoryId < 0` | `CategoryId must be 0 (or left out) to add a category, or the id of the category to edit.` |
| `organizationId <= 0` | `OrganizationId is required and must be greater than 0.` |
| `categoryName` blank | `CategoryName is required.` |
| `categoryName` longer than 50 | `CategoryName must be 50 characters or fewer.` |
| `createdBy <= 0` | `CreatedBy is required: the row records who created it.` |

```json
{
  "success": false,
  "message": "One or more validation errors occurred.",
  "data": null,
  "errors": [
    "OrganizationId: OrganizationId is required and must be greater than 0.",
    "CategoryName: CategoryName is required."
  ]
}
```

**404** — `categoryId > 0` names no live category of `organizationId`:

```json
{ "success": false, "message": "Project category '7' was not found.", "data": null, "errors": [] }
```

**500** — unhandled defect, or the procedure has not been created in the
database yet.

---

### 24. Remove Project Category

`POST /api/v1/Admin/remove-timesheet-project-category`

**Purpose** — Soft-delete a project category of an organisation. The row is
**never** removed: it is marked `IsDelete = 1` and stamped with `DeletedDate`
(UTC) and `Deletedby`. From then on the category list and Add / Edit Project
Category no longer see it. Done by the API through EF Core — no stored
procedure.

A POST rather than an HTTP DELETE, as with
[Delete User Role](#18-delete-user-role).

#### Request body — `ProjectCategoryDeleteRequest` (JSON)

| Field | Type | Required | Rules | Notes |
|---|---|---|---|---|
| `categoryId` | int | yes | `> 0` | |
| `organizationId` | int | yes | `> 0` | The category must belong to it — another organisation's category is a 404, never removed |
| `deletedBy` | int | yes | `> 0` | Written to `Deletedby`. **Temporary** — comes from the token once auth is on |

Validated in **`AdminService`**; every failing field is listed at once.

#### Example request

```json
{ "categoryId": 7, "organizationId": 12, "deletedBy": 5 }
```

#### Success response — `200 OK`

```json
{ "success": true, "message": "Project category removed.", "data": null, "errors": [] }
```

#### Error responses

**400 — validation:**

| Trigger | Message |
|---|---|
| `categoryId <= 0` | `CategoryId is required and must be greater than 0.` |
| `organizationId <= 0` | `OrganizationId is required and must be greater than 0.` |
| `deletedBy <= 0` | `DeletedBy is required: a soft delete records who performed it.` |

**404** — no live category with that id in that organisation. Removing one
twice is **not** a silent success, and the first `DeletedDate` is never
overwritten:

```json
{
  "success": false,
  "message": "Project category '7' was not found in organization '12'.",
  "data": null,
  "errors": []
}
```

**500** — unhandled defect.

---

## SheetSubmission

`SheetSubmissionController` — `api/v1/SheetSubmission`. Handing a week's
timesheet in, reviewing it, and reading it back.

A **sheet** is one user's timesheet week, named by the `sheetCode` every
`dbo.TimeLog` entry in that week carries (see
[The generated `sheetCode`](#the-generated-sheetcode)). Submitting it writes one
row to `dbo.TimesheetSubmission`, keyed by that code; every status change —
the submit itself, and every review — also writes one row to
`dbo.TimeSheetSubmissionAuditLog`, in the **same transaction** as the change.
See [`TimesheetSubmissionStatus`](#timesheetsubmissionstatus--the-statusid-column-of-dbotimesheetsubmission)
for the status values.

`orgId` is accepted by every endpoint here but **used only by the Submitted
Sheet List**; `adminId` is not checked against any role. See
[Known gaps](#known-gaps).

---

### 12. Submit Timesheet

`POST /api/v1/SheetSubmission/submit-timesheet`

**Purpose** — Submit one user's sheet. The totals are **not** in the payload:
the service adds them up from the user's **saved** entries on the sheet, so a
client can never submit a figure the entries do not support.

- Only `Save` entries count — **drafts and deleted entries are excluded**, and
  so is an entry missing any of its four date/time values.
- Each entry counts from start date + start time to end date + end time, so an
  overnight shift counts in full.
- The row is written with `statusId = 1` (Submitted), `submittedBy = userId` and
  `submittedDate` = now (UTC), and the same instant is written to the audit log.
- A sheet is submitted **once**; a second submit is a 409.

#### Request body — `SheetSubmissionRequest`

| Field | Type | Required | Rules | Notes |
|---|---|---|---|---|
| `sheetCode` | string | yes | not empty, `<= 15` chars | Trimmed before use |
| `userId` | int | yes | `> 0` | The submitter, whose entries are totalled. **Temporary** — comes from the token once auth is on |
| `orgId` | int | no | — | **Accepted but not used** |

#### Example request

```json
{
  "sheetCode": "SHT00012",
  "userId": 5,
  "orgId": 3
}
```

#### Success response — `200 OK`

`data` is a `SheetSubmissionResponse` — the stored row:

| Field | Type | Meaning |
|---|---|---|
| `sheetCode` | string | The submitted sheet (`Timesheetcode`) |
| `totalHours` | decimal | The **whole** total in hours, 2 places — `37.5` for 37h 30m |
| `totalMins` | decimal | The **whole** total in minutes — `2250` for 37h 30m (not the leftover minutes) |
| `submittedBy` | int | The submitter |
| `submittedDate` | datetime | When, in UTC |
| `statusId` | int? | `1` on a new submission |
| `approvedBy` | int? | Set by an approving review |
| `approvedDate` | datetime? | Set by an approving review |
| `rejectedBy` | int? | Set by a rejecting review |
| `rejectedDate` | datetime? | Set by a rejecting review |

#### Example response

```json
{
  "success": true,
  "message": "Timesheet submitted.",
  "data": {
    "sheetCode": "SHT00012",
    "totalHours": 37.5,
    "totalMins": 2250,
    "submittedBy": 5,
    "submittedDate": "2026-10-02T17:04:11.203Z",
    "statusId": 1,
    "approvedBy": null,
    "approvedDate": null,
    "rejectedBy": null,
    "rejectedDate": null
  },
  "errors": []
}
```

#### Error responses

**400 — shape validation:**

| Trigger | Message |
|---|---|
| `sheetCode` missing or blank | `SheetCode is required.` |
| `sheetCode` over 15 characters | `SheetCode must be 15 characters or fewer.` |
| `userId <= 0` | `UserId is required and must be greater than 0.` |

**400 — nothing to submit.** The user has no saved entry on the sheet — the code
does not exist, belongs to someone else, or holds only drafts:

```json
{
  "success": false,
  "message": "User '5' has no saved time logged on timesheet 'SHT00012'.",
  "data": null,
  "errors": []
}
```

**409 — already submitted.** Also returned for a **rejected** sheet — see
[Known gaps](#known-gaps):

```json
{
  "success": false,
  "message": "Timesheet 'SHT00012' has already been submitted.",
  "data": null,
  "errors": []
}
```

**500** — unhandled defect.

---

### 13. Review Timesheet

`POST /api/v1/SheetSubmission/review-timesheet`

**Purpose** — Approve or reject a submitted sheet. Sets the row's `statusId`,
stamps the admin and the time, and writes an audit log entry — in one
transaction.

A sheet can be reviewed **any number of times**: a rejected sheet can be
approved later, and the reverse. The row shows only the **latest** decision —
approving clears `rejectedBy` / `rejectedDate`, rejecting clears `approvedBy` /
`approvedDate` — and the audit log keeps every one of them, in order.

#### Request body — `SheetSubmissionReviewRequest`

| Field | Type | Required | Rules | Notes |
|---|---|---|---|---|
| `adminId` | int | yes | `> 0` | Written to `approvedBy` / `rejectedBy` and to the audit log's `CreatedBy`. **Not checked against any role** |
| `sheetCode` | string | yes | not empty, `<= 15` chars | The submitted sheet. Trimmed before use |
| `status` | int | yes | `2` or `3` **only** | `2` = Approved, `3` = Rejected. `1`, `0` (or missing) and anything else are a 400 |
| `orgId` | int | no | — | **Accepted but not used** |

#### Example request

```json
{
  "adminId": 1,
  "sheetCode": "SHT00012",
  "status": 2,
  "orgId": 3
}
```

#### Success response — `200 OK`

`data` is the `SheetSubmissionResponse` as it now stands — the same shape as
[Submit Timesheet](#12-submit-timesheet). `message` is `Timesheet approved.` or
`Timesheet rejected.`

```json
{
  "success": true,
  "message": "Timesheet approved.",
  "data": {
    "sheetCode": "SHT00012",
    "totalHours": 37.5,
    "totalMins": 2250,
    "submittedBy": 5,
    "submittedDate": "2026-10-02T17:04:11.203Z",
    "statusId": 2,
    "approvedBy": 1,
    "approvedDate": "2026-10-03T09:15:42.870Z",
    "rejectedBy": null,
    "rejectedDate": null
  },
  "errors": []
}
```

#### Error responses

**400 — shape validation:**

| Trigger | Message |
|---|---|
| `adminId <= 0` | `AdminId is required and must be greater than 0.` |
| `sheetCode` missing or blank | `SheetCode is required.` |
| `sheetCode` over 15 characters | `SheetCode must be 15 characters or fewer.` |
| `status` not `2` or `3` | `Status must be 2 (Approved) or 3 (Rejected).` |

```json
{
  "success": false,
  "message": "One or more validation errors occurred.",
  "data": null,
  "errors": [
    "Status: Status must be 2 (Approved) or 3 (Rejected)."
  ]
}
```

**404 — the sheet has not been submitted:**

```json
{
  "success": false,
  "message": "Timesheet 'SHT00012' has not been submitted.",
  "data": null,
  "errors": []
}
```

**500** — unhandled defect.

---

### 14. Current Week Sheet Details

`GET /api/v1/SheetSubmission/get-current-week-sheet-details/{userID}/{orgID}`

**Purpose** — The user's current timesheet week, as their admin setup defines
it: its dates and working days, its sheet code, and the hours worked against the
hours expected.

#### Route parameters

| Parameter | Type | Constraint |
|---|---|---|
| `userID` | int | `:int:min(1)` — `0` or below matches no route (**404**) |
| `orgID` | int | `:int` — **accepted but not used** |

No request body.

#### How the figures are worked out

- **"Current"** is today in the time zone the user's setup names (UTC when
  none, or when the zone is unrecognised) — the same week the time log save puts
  today's entries in.
- **`weekStartDate`** is the setup's `startDay` on or before today; Monday when
  there is no usable setup.
- **`weekEndDate`** is the setup's `endDay` after it — Friday for a
  Monday-to-Friday setup — or six days after the start without a usable setup.
  Note this differs from [User Dashboard Summary](#10-user-dashboard-summary),
  whose `weekEndDate` is always start + 6.
- **`sheetCode`** is the code the user's entries in the week carry. The code
  spans all seven days whatever `endDay` says, so a weekend entry on a
  Monday-to-Friday setup still counts toward `totalHoursWorked`.
- **`totalHoursWorked`** counts **saved** entries on the sheet only — exactly
  what [Submit Timesheet](#12-submit-timesheet) would record. The user dashboard
  counts drafts too, so its figure can be higher.
- **`totalHoursExpected`** is working days (`startDay`..`endDay`, wrapping past
  Sunday) × daily time (`maxTimeinhrs` hours + minutes, plus the minutes of
  `maxTiminmins`) — the dashboard's formula.
- **`totalHoursDrift`** is worked − expected: negative when short, positive when
  over.

#### Success response — `200 OK`

`data` is a `CurrentWeekSheetDetailResponse`:

| Field | Type | Meaning |
|---|---|---|
| `userId` | int | The user |
| `sheetCode` | string? | This week's code; **`null`** when nothing is logged this week yet |
| `weekStartDate` | datetime | First day of the week |
| `weekEndDate` | datetime | Last working day of the week |
| `startDay` | int? | The setup's `StartDay` as stored — 1 = Monday … 7 = Sunday; `null` with no setup |
| `startDayName` | string? | Its name — `"Monday"`; `null` when `startDay` is null or not 1–7 |
| `endDay` | int? | The setup's `EndDay` as stored |
| `endDayName` | string? | Its name — `"Friday"` |
| `totalHoursWorked` | decimal | Hours on the sheet, 2 places; `0` when there is no sheet yet |
| `totalHoursExpected` | decimal? | Hours the setup expects; `null` without a complete setup |
| `totalHoursDrift` | decimal? | Worked − expected; `null` whenever expected is |

#### Example request

```http
GET /api/v1/SheetSubmission/get-current-week-sheet-details/5/3
```

#### Example response

```json
{
  "success": true,
  "message": "",
  "data": {
    "userId": 5,
    "sheetCode": "SHT00012",
    "weekStartDate": "2026-09-28T00:00:00",
    "weekEndDate": "2026-10-02T00:00:00",
    "startDay": 1,
    "startDayName": "Monday",
    "endDay": 5,
    "endDayName": "Friday",
    "totalHoursWorked": 30.5,
    "totalHoursExpected": 40,
    "totalHoursDrift": -9.5
  },
  "errors": []
}
```

#### When the user has no setup, or has logged nothing

Still **`200`** — never a 404:

- **No setup:** a Monday-to-Sunday week; `startDay`, `startDayName`, `endDay`,
  `endDayName`, `totalHoursExpected` and `totalHoursDrift` are `null`.
- **Nothing logged this week:** `sheetCode` is `null` and `totalHoursWorked` is
  `0`.

#### Error responses

| Status | Cause |
|---|---|
| **404** | `userID` is `0` or below, or not an integer — no route matches |
| **500** | Unhandled defect |

---

### 15. Submitted Sheet List

`POST /api/v1/SheetSubmission/get-submitted-sheet-list`

**Purpose** — The admin's view of the sheets submitted in one organisation:
one row per submission, newest first, for every user or for one.

Backed by `dbo.spc_GetSubmittedSheetList`.

#### Request body — `SubmittedSheetListRequest`

| Field | Type | Required | Rules | Notes |
|---|---|---|---|---|
| `adminId` | int | yes | `> 0` | Passed to the procedure as `@PAdminID`. **Not checked yet** — the admin check is a placeholder |
| `userId` | int | yes | `>= 0` | **`0` = every user** in the organisation; any other value = that user only |
| `orgId` | int | yes | `> 0` | Filters on `dbo.Signup.OrganizationID` |

#### How the figures are worked out

The row carries the same week and hours fields as
[Current Week Sheet Details](#14-current-week-sheet-details), with three
deliberate differences:

- **`totalHoursWorked`** is the `TotalHours` **submitted**, not a recount — it
  stays what the user handed in even if the entries change afterwards.
- **The week** is dated from the sheet's **earliest live entry**, anchored on the
  setup's `startDay`. Both week dates are `null` when the sheet has no live entry
  left.
- **`totalHoursExpected` and `totalHoursDrift`** use the user's **current**
  setup, so changing a setup changes them for older sheets too.

#### Example request

```json
{
  "adminId": 1,
  "userId": 0,
  "orgId": 3
}
```

#### Success response — `200 OK`

`data` is a `SubmittedSheetResponse[]`:

| Field | Type | Meaning |
|---|---|---|
| `userId` | int | The submitter |
| `name` | string? | From `dbo.Signup` |
| `email` | string? | From `dbo.Signup` |
| `sheetCode` | string | The submitted sheet |
| `weekStartDate` | datetime? | The setup's `startDay` on or before the sheet's earliest entry |
| `weekEndDate` | datetime? | The setup's `endDay` after it |
| `startDay` | int? | The setup's `StartDay` — 1 = Monday … 7 = Sunday |
| `startDayName` | string? | Its name, from `dbo.DayMaster` |
| `endDay` | int? | The setup's `EndDay` |
| `endDayName` | string? | Its name, from `dbo.DayMaster` |
| `totalHoursWorked` | decimal | Hours submitted |
| `totalHoursExpected` | decimal? | Hours the current setup expects; `null` without a complete setup |
| `totalHoursDrift` | decimal? | Worked − expected; `null` whenever expected is |
| `status` | int? | 1 = Submitted, 2 = Approved, 3 = Rejected |
| `statusText` | string? | The status as text. **Only `"Submitted"` today** — `null` for 2 and 3 until the procedure's `CASE` is extended |
| `submittedDate` | datetime | When the sheet was submitted, in UTC |

#### Example response

```json
{
  "success": true,
  "message": "",
  "data": [
    {
      "userId": 5,
      "name": "Jane Doe",
      "email": "jane.doe@example.com",
      "sheetCode": "SHT00012",
      "weekStartDate": "2026-09-28T00:00:00",
      "weekEndDate": "2026-10-02T00:00:00",
      "startDay": 1,
      "startDayName": "Monday",
      "endDay": 5,
      "endDayName": "Friday",
      "totalHoursWorked": 37.5,
      "totalHoursExpected": 40,
      "totalHoursDrift": -2.5,
      "status": 1,
      "statusText": "Submitted",
      "submittedDate": "2026-10-02T17:04:11.203"
    }
  ],
  "errors": []
}
```

Nothing submitted is **`200`** with `data: []` — never a 404.

#### Error responses

**400 — shape validation:**

| Trigger | Message |
|---|---|
| `adminId <= 0` | `AdminId is required and must be greater than 0.` |
| `userId < 0` | `UserId must be 0 (every user) or a user id.` |
| `orgId <= 0` | `OrgId is required and must be greater than 0.` |

**500** — unhandled defect.

---

## MasterAdmin

Controller: `src/ETimeSheet.Api/Controllers/MasterAdminController.cs`
Service: `MasterAdminService` · Prefix: `/api/v1/MasterAdmin`

Client setup over `dbo.TimesheetClientSetup`: list, add / edit with a logo
upload, and soft delete — plus the organisation list from `dbo.Organization`.
Like Admin, it has **no permission** behind it while authentication is off.

**Every client belongs to an organisation.** `OrganizationId` is required on
add / edit and returned on every read. The client list is still **not**
filtered by organisation — it returns every live client.

**Currency is derived, never sent.** A client's `currencyId` is the
`dbo.CountryCurrency.CurrencyID` of its `countryId` (one row per country), stored
in the table's `Currencycode` column. `currencyName` and `currencySymbol` are
joined back from the same table on every read.

### The `ClientResponse` shape

Returned by all three read/write endpoints below, in this order:

| Field | Type | Notes |
|---|---|---|
| `clientId` | int | `IDENTITY` |
| `clientCode` | string | Generated on add — `CL0001`, `CL0002` … — and **never** changes |
| `clientName` | string | |
| `avatar` | string \| null | The uploaded logo's `path` — see the warning below |
| `billingEmail` | string | Unique among live clients |
| `phoneNumber` | string | |
| `alternatePhoneNumber` | string \| null | |
| `alternateEmail` | string \| null | |
| `billingAddress` | string \| null | |
| `paymentTermsDays` | int | |
| `currencyId` | int | `dbo.CountryCurrency.CurrencyID`, derived from the country |
| `currencyName` | string \| null | `"Indian Rupee"`. `null` only for a client saved before currencies were derived |
| `currencySymbol` | string \| null | `"₹"`. **Not unique** — `"$"` is shared by many currencies; show it beside `currencyName` |
| `countryId` | int \| null | `dbo.Country.ID` |
| `taxIdentifier` | string \| null | |
| `organizationId` | int | The organisation the client belongs to |

`isDeleted` and `deleteDate` are **never** returned — deleted clients never
appear.

> **`avatar` expires.** The document service returns a pre-signed S3 URL valid
> for **48 hours** (`X-Amz-Expires=172800`). It is stored as returned, so two
> days after an upload the stored link stops opening. See
> [Known gaps](#known-gaps).

---

### 19. Client List

`GET /api/v1/MasterAdmin/get-client-list/{orgID}`

**Purpose** — The live clients (`IsDeleted = 0`) of one organisation, ordered
by name, then id. Read through `dbo.spc_GetClientListForMasterAdmin`.

#### Parameters

| Parameter | In | Type | Required | Rules |
|---|---|---|---|---|
| `orgID` | route | int | yes | `> 0`. Omitted, the URL matches no route and is a `404` |

No paging.

#### Example request

```
GET /api/v1/MasterAdmin/get-client-list/12
```

#### Success response — `200 OK`

`data` is an array of [`ClientResponse`](#the-clientresponse-shape).

#### Example response

```json
{
  "success": true,
  "message": "",
  "data": [
    {
      "clientId": 1,
      "clientCode": "CL0001",
      "clientName": "Acme Consulting",
      "avatar": "https://ppmdocs.s3.ap-south-1.amazonaws.com/12/b941…_logo.jpeg?X-Amz-Expires=172800&…",
      "billingEmail": "billing@acme.test",
      "phoneNumber": "+91 98765 43210",
      "alternatePhoneNumber": null,
      "alternateEmail": null,
      "billingAddress": "12 MG Road, Bengaluru",
      "paymentTermsDays": 30,
      "currencyId": 102,
      "currencyName": "Indian Rupee",
      "currencySymbol": "₹",
      "countryId": 103,
      "taxIdentifier": "29ABCDE1234F1Z5",
      "organizationId": 12
    }
  ],
  "errors": []
}
```

#### Empty result

An organisation with no clients is `data: []`.

#### Error responses

**400** — `orgID` is `0` or negative:

```json
{
  "success": false,
  "message": "One or more validation errors occurred.",
  "data": null,
  "errors": [
    "orgID: orgID is required and must be greater than 0."
  ]
}
```

**500** — unhandled defect, or `spc_GetClientListForMasterAdmin` does not yet
take `@POrgID` in the database.

---

### 20. Add / Edit Client Setup

`POST /api/v1/MasterAdmin/add-or-edit-client-setup`

**Purpose** — Add a client, or edit one, optionally uploading its logo. **One
endpoint for both**, chosen by `clientId`. Written through
`dbo.spc_AddEditClientSetup`.

**Sent as `multipart/form-data`, not JSON** — a JSON body is refused with
`415 Unsupported Media Type`.

| `clientId` sent | Result |
|---|---|
| absent or `0` | **Adds** a client with the next code: `CL0001`, `CL0002` … `CL9999`, then `CL10000`. The sequence counts deleted clients, so a code is never reissued, and is read under a table lock, so two adds at once never share a code |
| `> 0`, a live client | **Edits** it. Its `clientCode` never changes |
| `> 0`, missing or deleted | **404** — before anything is uploaded |

#### Form fields — `ClientSaveRequest`

| Field | Type | Required | Rules | Notes |
|---|---|---|---|---|
| `ClientId` | int | no | `>= 0` | absent / `0` adds; an id edits |
| `ClientName` | string | yes | not blank, `<= 50` | |
| `ClientLogo` | file | no | not empty | See [The logo](#the-logo) |
| `BillingEmail` | string | yes | valid email, `<= 150` | **Unique** among live clients, ignoring case |
| `PhoneNumber` | string | yes | not blank, `<= 150` | |
| `AlternatePhoneNumber` | string | no | `<= 150` | |
| `AlternateEmail` | string | no | valid email when sent, `<= 150` | Not checked for uniqueness |
| `BillingAddress` | string | no | any length | `nvarchar(max)` |
| `PaymentTermsDays` | int | yes | `>= 0` | |
| `CountryId` | int | yes | `> 0`, and must have a currency | The currency is taken from it; changing it on edit changes the currency |
| `TaxIdentifier` | string | no | `<= 50` | |
| `OrganizationId` | int | **yes** | `> 0` | The organisation the client belongs to. Written on add and on edit |

There is **no** `clientCode`, `avatar`, `currencyCode` / `currencyId`,
`isDeleted` or `deleteDate` field — all are generated, derived or managed by the
API. Field names are case-insensitive (`Clientlogo` binds too).

#### The logo

When `ClientLogo` is sent, the API uploads it to the document service and
stores the returned `path` as the client's `avatar`:

- **Endpoint:** `POST {EmailService:BaseUrl}/api/Upload/Updatedoc/TSCLT`,
  `multipart/form-data` with one part named `file`. The base URL is
  `EmailService:BaseUrl` in `appsettings.json`; the path and the `TSCLT` project
  code are constants (`ExternalServiceEndpoints.Upload.UpdateDocument`,
  `DocumentProjectCodes.ClientSetup`).
- **Answer read:** `{ "success": true, "message": "File uploaded", "path": "…" }`.
  `success: false`, or no `path`, is a 502 and nothing is saved.
- **Edit without a logo** keeps the client's current avatar. **Add without a
  logo** leaves it `null`. There is no way to clear a logo once set.
- **Order:** validation → country has a currency → client exists (edit) →
  billing email free → **upload** → save. Every refusal before the upload leaves
  no file behind; a failure *after* it (rare) leaves the uploaded file on the
  service.

#### Example request — add with a logo

```bash
curl -X POST 'http://localhost:5041/api/v1/MasterAdmin/add-or-edit-client-setup' \
  -H 'accept: application/json' \
  -F 'ClientId=0' \
  -F 'ClientName=Acme Consulting' \
  -F 'ClientLogo=@acme-logo.jpg;type=image/jpeg' \
  -F 'BillingEmail=billing@acme.test' \
  -F 'PhoneNumber=+91 98765 43210' \
  -F 'PaymentTermsDays=30' \
  -F 'CountryId=103' \
  -F 'BillingAddress=12 MG Road, Bengaluru' \
  -F 'TaxIdentifier=29ABCDE1234F1Z5' \
  -F 'OrganizationId=12'
```

#### Example request — edit, keeping the logo

```bash
curl -X POST 'http://localhost:5041/api/v1/MasterAdmin/add-or-edit-client-setup' \
  -F 'ClientId=1' \
  -F 'ClientName=Acme Consulting Pvt Ltd' \
  -F 'BillingEmail=billing@acme.test' \
  -F 'PhoneNumber=+91 98765 43210' \
  -F 'PaymentTermsDays=45' \
  -F 'CountryId=103' \
  -F 'OrganizationId=12'
```

#### Success response — `200 OK`

`data` is the saved [`ClientResponse`](#the-clientresponse-shape), including the
new `clientId`, `clientCode`, `avatar` and currency:

```json
{
  "success": true,
  "message": "Client setup saved.",
  "data": {
    "clientId": 1,
    "clientCode": "CL0001",
    "clientName": "Acme Consulting",
    "avatar": "https://ppmdocs.s3.ap-south-1.amazonaws.com/12/b941…_acme-logo.jpg?X-Amz-Expires=172800&…",
    "billingEmail": "billing@acme.test",
    "phoneNumber": "+91 98765 43210",
    "alternatePhoneNumber": null,
    "alternateEmail": null,
    "billingAddress": "12 MG Road, Bengaluru",
    "paymentTermsDays": 30,
    "currencyId": 102,
    "currencyName": "Indian Rupee",
    "currencySymbol": "₹",
    "countryId": 103,
    "taxIdentifier": "29ABCDE1234F1Z5",
    "organizationId": 12
  },
  "errors": []
}
```

#### Error responses

**400 — form validation** (every failure listed):

| Trigger | Message |
|---|---|
| `ClientId < 0` | `ClientId must be 0 (or left out) to add a client, or the id of the client to edit.` |
| `ClientName` blank / too long | `ClientName is required.` / `ClientName must be 50 characters or fewer.` |
| `ClientLogo` empty file | `ClientLogo is an empty file.` |
| `BillingEmail` blank / too long / malformed | `BillingEmail is required.` / `BillingEmail must be 150 characters or fewer.` / `BillingEmail must be a valid email address.` |
| `PhoneNumber` blank / too long | `PhoneNumber is required.` / `PhoneNumber must be 150 characters or fewer.` |
| `AlternatePhoneNumber` too long | `AlternatePhoneNumber must be 150 characters or fewer.` |
| `AlternateEmail` too long / malformed | `AlternateEmail must be 150 characters or fewer.` / `AlternateEmail must be a valid email address.` |
| `PaymentTermsDays` missing / negative | `PaymentTermsDays is required.` / `PaymentTermsDays must be 0 or more.` |
| `CountryId` missing / not positive | `CountryId is required: the client's currency is taken from its country.` / `CountryId must be greater than 0.` |
| `TaxIdentifier` too long | `TaxIdentifier must be 50 characters or fewer.` |
| `OrganizationId` missing | `OrganizationId is required: a client belongs to an organisation.` |
| `OrganizationId <= 0` | `OrganizationId must be greater than 0.` |

**400 — the country has no currency** (Antarctica, `CountryId = 8`):

```json
{
  "success": false,
  "message": "One or more validation errors occurred.",
  "data": null,
  "errors": ["CountryId: Country '8' has no currency set up, so a client cannot be saved against it."]
}
```

**404** — `ClientId > 0` names a client that does not exist or is deleted:

```json
{ "success": false, "message": "Client '42' was not found.", "data": null, "errors": [] }
```

**409** — another live client already has that billing email (case-insensitive;
the client being edited may keep its own):

```json
{
  "success": false,
  "message": "A client with the billing email 'billing@acme.test' already exists.",
  "data": null,
  "errors": []
}
```

**415** — the body was sent as JSON instead of `multipart/form-data`.

**502** — the logo could not be stored; **nothing was saved**:

| Cause | Message |
|---|---|
| Service unreachable | `The document service could not be reached.` |
| Service too slow | `The document service did not answer within 60 seconds.` |
| Service answered an error status | `The document service refused the upload (<status> <reason>).` |
| Answer not JSON | `The document service answered in a format this API does not understand.` |
| `success: false`, or no `path` | `The document service did not accept the upload, or did not say where the file was stored.` |
| `path` longer than the 500-character `Avatar` column | `The logo was uploaded, but its path is <n> characters long and the client's Avatar column holds at most 500. Nothing was saved.` |

The service's own response body is written to the log, never to the caller.

**500** — unhandled defect.

---

### 21. Remove Client Setup

`POST /api/v1/MasterAdmin/remove-client-setup`

**Purpose** — Soft-delete a client. The row is **never** removed: it is marked
`IsDeleted = 1` and stamped with `DeleteDate` (UTC). From then on the client
list and Add / Edit Client Setup no longer see it; its `clientCode` stays spent,
and its billing email is free to reuse. Done by the API through EF Core — no
stored procedure.

#### Request body — `ClientDeleteRequest` (JSON)

| Field | Type | Required | Rules |
|---|---|---|---|
| `clientId` | int | yes | `> 0`; a live client |

#### Example request

```json
{ "clientId": 5 }
```

#### Success response — `200 OK`

```json
{ "success": true, "message": "Client setup removed.", "data": null, "errors": [] }
```

#### Error responses

**400** — `clientId <= 0`: `ClientId: ClientId is required and must be greater than 0.`

**404** — no live client has that id. Removing one twice is **not** a silent
success, and the first `DeleteDate` is never overwritten:

```json
{ "success": false, "message": "Client '5' was not found.", "data": null, "errors": [] }
```

**500** — unhandled defect.

---

### 32. Organization List

`GET /api/v1/MasterAdmin/get-all-organization`

**Purpose** — Every organisation in `dbo.Organization`, for an organisation
picker — for example the `OrganizationId` of
[Add / Edit Client Setup](#20-add--edit-client-setup). Read through
`dbo.spc_GetOrganizationList`.

#### Parameters

None.

#### Example request

```
GET /api/v1/MasterAdmin/get-all-organization
```

#### Success response — `200 OK`

`data` is an array of `OrganizationResponse`, ordered by `organizationName`,
then `organizationId`:

| Field | Type | Notes |
|---|---|---|
| `organizationId` | int | `dbo.Organization.OrganizationID` |
| `organizationName` | string \| null | Nullable on the table |
| `countryId` | int \| null | |

```json
{
  "success": true,
  "message": "",
  "data": [
    { "organizationId": 12, "organizationName": "Acme Corp", "countryId": 103 },
    { "organizationId": 7, "organizationName": "Globex", "countryId": 236 }
  ],
  "errors": []
}
```

#### Empty result

No organisations is `data: []` — a 200, not a 404.

#### Error responses

**500** — unhandled defect, or the procedure has not been created in the
database yet.

---

## TimesheetProject

`TimesheetProjectController` — `api/v1/TimesheetProject`. An organisation's
projects (`dbo.TimesheetProject`), its project statuses
(`dbo.TimesheetProjectStatus`), each project's budget
(`dbo.TimesheetProjectBudget`), its timesheet users' project assignments
(`dbo.Timesheet_Users_Project_setup`), its holidays
(`dbo.TimesheetHolidaysetup`), each project's weekend days
(`dbo.TimeSheet_Weekend_setup`), which modules each user may use
(`dbo.Timesheet_role_assignment`), the time users log on projects
(`dbo.TimesheetProjectTimeLog`), and the project timesheets that time is
submitted on (`dbo.TimesheetProjectSubmission`, with every status change in
`dbo.TimesheetProjectSubmissionAuditLog`).

In the controller the actions are grouped into regions — Project, Project
Budget, Project Status, Timesheet User, Holiday Setup, Weekend Setup, User
Access Setup, Project Time Log and Project Time Sheet. The grouping is source
layout only; it changes no route.

**A project and its budget are one-to-one.** A budget row belongs to exactly
one project (its single `ProjectID`), and a project has at most one budget:

| Rule | How the API keeps it |
|---|---|
| One budget → one project | The save takes no `budgetId`, only `projectId`, and an update never changes a budget's project — a budget cannot be moved or shared |
| One project → one budget | [Add / Edit Project Budget](#30-add--edit-project-budget) updates the project's existing budget and adds one only when it has none, under a lock, so two saves cannot both add |

Neither rule is enforced by the table — see [Known gaps](#known-gaps) 34.

Validation for every endpoint here runs in **`TimesheetProjectService`**, not a
FluentValidation validator. The 400 is the same shape and lists **every**
failing field at once.

**Project and budget removes are hard deletes.** Neither
`dbo.TimesheetProject` nor `dbo.TimesheetProjectBudget` has a soft-delete or
audit column, so a removed row is physically gone and nothing records who
removed it. **A status remove is a soft delete**: `dbo.TimesheetProjectStatus`
keeps the row, stamped with `DeletedDate` and `Deletedby`.

| Remove | Kind | What is recorded |
|---|---|---|
| Project, project budget | hard delete | nothing |
| Project status | soft delete | `DeletedDate`, `Deletedby` |
| Timesheet user | soft delete | `IsDelete = 1`, `DeletedDate`, `DeletedBy` |
| Holiday | soft delete | `IsDelete = 1` only — the table has no delete stamps |
| Weekend setup | soft delete | `IsDelete = 1`, `DeletedDate`, `DeletedBy` |
| User access setup | **hard delete** | nothing — the table has no soft-delete column |
| Project time log | soft delete | `IsDelete = 1`, `DeletedDate`, `DeletedBy` |

---

### 25. Project List

`GET /api/v1/TimesheetProject/get-timesheet-project-by-orgID/{orgID}`

**Purpose** — Every project of one organisation, each with its status name,
client name and currency symbol.
Read through `dbo.spc_GetTimesheetProject`.

#### Route parameters

| Name | Type | Rules |
|---|---|---|
| `orgID` | int | `> 0` — checked by the service, so a `0` is a 400 naming the field rather than a missed route |

#### Example request

```
GET /api/v1/TimesheetProject/get-timesheet-project-by-orgID/12
```

#### Success response — `200 OK`

`data` is an array of `TimesheetProjectResponse`, ordered by `projectName`,
then `projectId`:

| Field | Type | Notes |
|---|---|---|
| `projectId` | int | `dbo.TimesheetProject.ProjectID` |
| `code` | string \| null | Generated on add — `PR0001`, `PR0002` … — and never changed |
| `projectName` | string \| null | The `Projectname` column |
| `totalBudget` | decimal \| null | `decimal(18,2)` |
| `totalHours` | decimal \| null | A **number of hours** (`37.5` is 37½ hours), may exceed 24 |
| `startDate` | datetime \| null | |
| `endDate` | datetime \| null | |
| `currencyCode` | int \| null | The client's currency — `dbo.CountryCurrency.CurrencyID`, copied from the client on every save |
| `currencySymbol` | string \| null | `dbo.CountryCurrency.CurrencySymbol` of `currencyCode` — `₹`, `€`, `$` …; `null` when the currency is unknown. Not unique |
| `status` | int \| null | A `statusId` from [Project Status List](#27-project-status-list) |
| `statusName` | string \| null | `Open` / `Closed` / `Pending`; `null` when `status` is null or unknown |
| `clientId` | int | The client the project is for — `dbo.TimesheetClientSetup.ClientID` |
| `clientName` | string \| null | The client's name — shown even if the client has since been removed; `null` only when the id names no client |
| `submissionFrequency` | int \| null | How often timesheets are submitted: `1` weekly, `2` every 15 days, `3` monthly; `null` when not set |
| `submissionFrequencyName` | string \| null | `submissionFrequency` as text: `Weekly`, `Every 15 Days` or `Monthly`; `null` when not set. Worked out by the procedure, not stored |

`organizationId` is **not** returned — the caller already sent it in the URL.

#### Example response

```json
{
  "success": true,
  "message": "",
  "data": [
    {
      "projectId": 4,
      "code": "PR0004",
      "projectName": "Website redesign",
      "totalBudget": 25000.00,
      "totalHours": 320.50,
      "startDate": "2026-10-01T00:00:00",
      "endDate": "2026-12-31T00:00:00",
      "currencyCode": 1,
      "currencySymbol": "؋",
      "status": 1,
      "statusName": "Open",
      "clientId": 5,
      "clientName": "Acme Ltd",
      "submissionFrequency": 1,
      "submissionFrequencyName": "Weekly"
    }
  ],
  "errors": []
}
```

#### Empty result

An organisation with no projects is `data: []` — a 200, not a 404.

#### Error responses

**400** — `orgID <= 0`: `orgID: orgID is required and must be greater than 0.`

**500** — unhandled defect, or the procedure has not been created in the
database yet.

---

### 26. Add / Edit Project

`POST /api/v1/TimesheetProject/add-edit-timesheet-project`

**Purpose** — Add a project to an organisation, or update one. **One endpoint
for both**, chosen by `projectId`. Written through
`dbo.spc_AddEditTimesheetProject`.

| `projectId` sent | Result |
|---|---|
| absent or `0` | **Adds** a project to `organizationId`, with the next code in the sequence `PR0001`, `PR0002` … `PR9999`, `PR10000` … |
| `> 0`, a project of `organizationId` | **Updates** it. `code` and `organizationId` never change |
| `> 0`, but no such project, or another organisation's | **404** — never edited |

**No `code` is sent.** The database generates it on add, as the highest
existing `PR` + digits code **across every organisation** plus one, under a
lock so two adds at once cannot get the same code. One sequence for the whole
table, as with client codes.

**No `currencyCode` is sent either.** The project's currency is the client's:
it is copied from the `Currencycode` of `clientId` on every save, so changing
the client on an update changes the currency too. A `currencyCode` in the body
is ignored.

**An update overwrites every field.** A field left out is saved as `null`, not
left as it was — send the whole project.

#### Request body — `TimesheetProjectSaveRequest` (JSON)

| Field | Type | Required | Rules | Notes |
|---|---|---|---|---|
| `projectId` | int \| null | no | `>= 0` | absent / `0` adds; an id updates |
| `organizationId` | int | yes | `> 0` | On update, the project must belong to it |
| `projectName` | string \| null | no | `<= 100` characters **after trimming** | Blank is saved as `null` |
| `totalBudget` | decimal \| null | no | `>= 0` | Two decimal places are stored |
| `totalHours` | decimal \| null | no | `>= 0` | A **number of hours**, not `hh:mm:ss` — `120.5`. May exceed 24 |
| `startDate` | datetime \| null | no | | |
| `endDate` | datetime \| null | no | not before `startDate` when both are sent | |
| `status` | int \| null | no | `> 0` when sent | A `statusId` from [Project Status List](#27-project-status-list). **Not** checked against the organisation's statuses |
| `clientId` | int | **yes** | `> 0`; a **live** (not deleted) client | The client the project is for, and the source of its currency. An update can change it |
| `submissionFrequency` | int \| null | no | `1`, `2` or `3` when sent | How often timesheets are submitted: `1` weekly, `2` every 15 days, `3` monthly. Left out, it is saved as `null` — on an update too |

#### Example request — add

```json
{
  "projectId": 0,
  "organizationId": 12,
  "projectName": "Website redesign",
  "totalBudget": 25000.00,
  "totalHours": 320.5,
  "startDate": "2026-10-01T00:00:00",
  "endDate": "2026-12-31T00:00:00",
  "status": 1,
  "clientId": 5,
  "submissionFrequency": 1
}
```

#### Example request — update

```json
{
  "projectId": 4,
  "organizationId": 12,
  "projectName": "Website redesign - phase 2",
  "totalBudget": 30000.00,
  "totalHours": 400,
  "startDate": "2026-10-01T00:00:00",
  "endDate": "2027-01-31T00:00:00",
  "status": 3,
  "clientId": 5,
  "submissionFrequency": 2
}
```

#### Success response — `200 OK`

`data` is a `TimesheetProjectResponse` — the saved project, with its
`projectId` and generated `code`; the same shape as
[Project List](#25-project-list):

```json
{
  "success": true,
  "message": "Project saved.",
  "data": {
    "projectId": 4,
    "code": "PR0004",
    "projectName": "Website redesign",
    "totalBudget": 25000.00,
    "totalHours": 320.50,
    "startDate": "2026-10-01T00:00:00",
    "endDate": "2026-12-31T00:00:00",
    "currencyCode": 1,
    "currencySymbol": "؋",
    "status": 1,
    "statusName": "Open",
    "clientId": 5,
    "clientName": "Acme Ltd",
    "submissionFrequency": 1,
    "submissionFrequencyName": "Weekly"
  },
  "errors": []
}
```

#### Error responses

**400 — validation** (every failure listed):

| Trigger | Message |
|---|---|
| `projectId < 0` | `ProjectId must be 0 (or left out) to add a project, or the id of the project to update.` |
| `organizationId <= 0` | `OrganizationId is required and must be greater than 0.` |
| `projectName` longer than 100 | `ProjectName must be 100 characters or fewer.` |
| `totalBudget < 0` | `TotalBudget must not be negative.` |
| `totalHours < 0` | `TotalHours must not be negative.` |
| `endDate` before `startDate` | `EndDate must not be before StartDate.` |
| `status <= 0` | `Status must be greater than 0 when sent.` |
| `clientId <= 0` (or left out) | `ClientId is required and must be greater than 0.` |
| `submissionFrequency` not 1, 2 or 3 | `SubmissionFrequency must be 1 (weekly), 2 (every 15 days) or 3 (monthly) when sent.` |

**400** — `clientId` names no client, or a removed one (checked after the shape
checks; a single entry):

```json
{
  "success": false,
  "message": "One or more validation errors occurred.",
  "data": null,
  "errors": ["ClientId: Client '5' does not exist or has been removed."]
}
```

**404** — `projectId > 0` names no project of `organizationId`:

```json
{ "success": false, "message": "Project '4' was not found.", "data": null, "errors": [] }
```

**500** — unhandled defect, the procedure has not been created in the
database yet, or (error `50001`) the code sequence has outgrown the
20-character `Code` column.

---

### 27. Project Status List

`GET /api/v1/TimesheetProject/get-timesheet-project-status-by-orgID/{orgID}`

**Purpose** — The live project statuses of one organisation, for a status
picker. Deleted statuses (`DeletedDate` set) are not included. The `statusId`
is what [Add / Edit Project](#26-add--edit-project) takes as `status`. Read
through `dbo.spc_GetTimesheetProjectStatus`.

#### Parameters

| Parameter | In | Type | Required | Rules |
|---|---|---|---|---|
| `orgID` | route | int | yes | `> 0`. Omitted, the URL matches no route and is a `404` |

#### Example request

```
GET /api/v1/TimesheetProject/get-timesheet-project-status-by-orgID/12
```

#### Success response — `200 OK`

`data` is an array of `TimesheetProjectStatusResponse`, ordered by `statusId`
— the order the organisation added them in:

| Field | Type | Notes |
|---|---|---|
| `statusId` | int | `dbo.TimesheetProjectStatus.StatusID` |
| `status` | string | The status's name |
| `colorCode` | string \| null | The display colour, as saved |

```json
{
  "success": true,
  "message": "",
  "data": [
    { "statusId": 1, "status": "Open", "colorCode": "#1E88E5" },
    { "statusId": 2, "status": "Closed", "colorCode": null }
  ],
  "errors": []
}
```

#### Empty result

An organisation with no statuses is `data: []`.

#### Error responses

**400** — `orgID <= 0`: `orgID: orgID is required and must be greater than 0.`

**500** — unhandled defect, or the procedure does not yet take `@POrgID` in
the database.

---

### 28. Remove Project

`POST /api/v1/TimesheetProject/remove-timesheet-project`

**Purpose** — Delete a project of an organisation **and its budget**. Both are
**hard** deletes, done together in one transaction — both go or neither does —
by the API through EF Core; no stored procedure. Every budget row of the
project is removed, duplicates included.

A POST rather than an HTTP DELETE, as with
[Remove Project Category](#24-remove-project-category).

#### Request body — `TimesheetProjectDeleteRequest` (JSON)

| Field | Type | Required | Rules | Notes |
|---|---|---|---|---|
| `projectId` | int | yes | `> 0` | |
| `organizationId` | int | yes | `> 0` | The project must belong to it — another organisation's project is a 404, never removed |

#### Example request

```json
{ "projectId": 4, "organizationId": 12 }
```

#### Success response — `200 OK`

```json
{ "success": true, "message": "Project removed.", "data": null, "errors": [] }
```

#### Error responses

**400 — validation:**

| Trigger | Message |
|---|---|
| `projectId <= 0` | `ProjectId is required and must be greater than 0.` |
| `organizationId <= 0` | `OrganizationId is required and must be greater than 0.` |

**404** — no project with that id in that organisation. Removing one twice is
**not** a silent success:

```json
{
  "success": false,
  "message": "Project '4' was not found in organization '12'.",
  "data": null,
  "errors": []
}
```

**500** — unhandled defect.

---

### 29. Project Budget List

`GET /api/v1/TimesheetProject/get-timesheet-project-budget-by-projectID/{projectID}`

**Purpose** — The budget of one project, with its category name. Read through
`dbo.spc_GetTimesheetProjectBudget`.

A project has **one** budget, so the list normally holds one item. It is still
a list: rows saved before the one-budget rule may give a project more than one,
and none of them is hidden.

#### Route parameters

| Name | Type | Rules |
|---|---|---|
| `projectID` | int | `> 0` — checked by the service |

#### Example request

```
GET /api/v1/TimesheetProject/get-timesheet-project-budget-by-projectID/4
```

#### Success response — `200 OK`

`data` is an array of `TimesheetProjectBudgetResponse`, ordered by
`periodStart`, then `budgetId`:

| Field | Type | Notes |
|---|---|---|
| `budgetId` | int | `dbo.TimesheetProjectBudget.BudgetID` |
| `projectCode` | string \| null | The project's `code`, copied on save. `null` on a row not saved since the column was added |
| `projectCategoryId` | int \| null | |
| `categoryName` | string \| null | From `dbo.TimesheetProjectCategory`; `null` when the category is null, unknown or deleted |
| `allocatedHours` | decimal \| null | A number of hours |
| `hourlyRate` | decimal \| null | The `Hourlyrate` column |
| `allocatedAmount` | decimal \| null | The `Allocated_amount` column — hours × rate, worked out on save |
| `periodStart` | datetime \| null | A date; the time part is always midnight |
| `periodEnd` | datetime \| null | A date; the time part is always midnight |

`projectId` is **not** returned — the caller already sent it in the URL.

#### Example response

```json
{
  "success": true,
  "message": "",
  "data": [
    {
      "budgetId": 8,
      "projectCode": "PR0004",
      "projectCategoryId": 3,
      "categoryName": "Development",
      "allocatedHours": 120.50,
      "hourlyRate": 45.00,
      "allocatedAmount": 5422.50,
      "periodStart": "2026-10-01T00:00:00",
      "periodEnd": "2026-12-31T00:00:00"
    }
  ],
  "errors": []
}
```

#### Empty result

A project with no budget — or no such project — is `data: []`, a 200.

#### Error responses

**400** — `projectID <= 0`: `projectID: projectID is required and must be greater than 0.`

**500** — unhandled defect, or the procedure has not been created in the
database yet.

---

### 30. Add / Edit Project Budget

`POST /api/v1/TimesheetProject/add-edit-timesheet-project-budget`

**Purpose** — Save the budget of a project. **One endpoint for both add and
update**, and **the project decides** — there is no budget id. Written through
`dbo.spc_AddEditTimesheetProjectBudget`.

| The project … | Result |
|---|---|
| has no budget | **Adds** one. `BudgetID` is `IDENTITY` — it comes back in the response |
| has a budget | **Updates** it. Should it hold more than one row from before the rule, the lowest `budgetId` is the one updated |
| does not exist | **404** |

The lookup runs under a lock, so two saves for one project at the same moment
cannot both add a row — the second waits, then updates.

Neither of these is sent; both are derived:

- **`projectCode`** — copied from the project's `code` by the procedure, on add
  and on every update.
- **`allocatedAmount`** — `allocatedHours × hourlyRate`, rounded to two decimal
  places (half away from zero); `null` when either is missing.

**An update overwrites every field.** A field left out is saved as `null` —
send the whole budget.

#### Request body — `TimesheetProjectBudgetSaveRequest` (JSON)

| Field | Type | Required | Rules | Notes |
|---|---|---|---|---|
| `projectId` | int | yes | `> 0`; the project must exist | Decides add or update |
| `projectCategoryId` | int \| null | no | `> 0` when sent; a **live** category of the **project's** organisation | |
| `allocatedHours` | decimal \| null | no | `>= 0` | A number of hours, not `hh:mm:ss`. May exceed 24 |
| `hourlyRate` | decimal \| null | no | `>= 0` | |
| `periodStart` | datetime \| null | no | | Only the date is stored |
| `periodEnd` | datetime \| null | no | not before `periodStart` when both are sent | Only the date is stored |

#### Example request

```json
{
  "projectId": 4,
  "projectCategoryId": 3,
  "allocatedHours": 120.5,
  "hourlyRate": 45.00,
  "periodStart": "2026-10-01",
  "periodEnd": "2026-12-31"
}
```

#### Success response — `200 OK`

`data` is a `TimesheetProjectBudgetResponse` — the saved budget, the same shape
as [Project Budget List](#29-project-budget-list):

```json
{
  "success": true,
  "message": "Project budget saved.",
  "data": {
    "budgetId": 8,
    "projectCode": "PR0004",
    "projectCategoryId": 3,
    "categoryName": "Development",
    "allocatedHours": 120.50,
    "hourlyRate": 45.00,
    "allocatedAmount": 5422.50,
    "periodStart": "2026-10-01T00:00:00",
    "periodEnd": "2026-12-31T00:00:00"
  },
  "errors": []
}
```

#### Error responses

**400 — validation** (every shape failure listed):

| Trigger | Message |
|---|---|
| `projectId <= 0` | `ProjectId is required and must be greater than 0.` |
| `projectCategoryId <= 0` | `ProjectCategoryId must be greater than 0 when sent.` |
| `allocatedHours < 0` | `AllocatedHours must not be negative.` |
| `hourlyRate < 0` | `HourlyRate must not be negative.` |
| `periodEnd` before `periodStart` | `PeriodEnd must not be before PeriodStart.` |

**400** — the category is not a live category of the project's organisation
(checked after the project is found; a single entry):

```json
{
  "success": false,
  "message": "One or more validation errors occurred.",
  "data": null,
  "errors": ["ProjectCategoryId: Project category '9' is not a category of this project's organization."]
}
```

**404** — the project does not exist:

```json
{ "success": false, "message": "Project '4' was not found.", "data": null, "errors": [] }
```

**500** — unhandled defect, or the procedure has not been created in the
database yet.

---

### 31. Remove Project Budget

`POST /api/v1/TimesheetProject/remove-timesheet-project-budget`

**Purpose** — Delete one budget, named by its `budgetId`. Only that row is
removed; any other budget row of the same project (a duplicate from before the
one-budget rule) is left alone. The project is kept, and can be given a new
budget with [Add / Edit Project Budget](#30-add--edit-project-budget). A **hard**
delete, by the API through EF Core; no stored procedure.

Removing the project itself ([Remove Project](#28-remove-project)) removes its
budget too; this endpoint removes the budget and keeps the project.

#### Request body — `TimesheetProjectBudgetDeleteRequest` (JSON)

| Field | Type | Required | Rules |
|---|---|---|---|
| `budgetId` | int | yes | `> 0` |

#### Example request

```json
{ "budgetId": 12 }
```

#### Success response — `200 OK`

```json
{ "success": true, "message": "Project budget removed.", "data": null, "errors": [] }
```

#### Error responses

**400** — `budgetId <= 0`: `BudgetId: BudgetId is required and must be greater than 0.`

**404** — no budget has this id. Removing one twice is **not** a silent
success:

```json
{ "success": false, "message": "Budget '12' was not found.", "data": null, "errors": [] }
```

**500** — unhandled defect.

---

### 33. Add / Edit Project Status

`POST /api/v1/TimesheetProject/add-edit-timesheet-project-status`

**Purpose** — Add a project status to an organisation, or edit one — **one
endpoint for both**. `statusId` `0` (or absent) adds; an existing
`statusId` edits that status, which must belong to `organizationId` and be
live. An edit overwrites `status` and `colorCode`, so send both. Written
through `dbo.spc_AddEditTimesheetProjectStatus`.

#### Request body — `TimesheetProjectStatusSaveRequest` (JSON)

| Field | Type | Required | Rules |
|---|---|---|---|
| `statusId` | int \| null | no | `0` / absent to add; `> 0` to edit |
| `organizationId` | int | yes | `> 0` |
| `status` | string | yes | Not blank, at most 20 characters; trimmed |
| `colorCode` | string \| null | no | At most 25 characters; stored as sent. Blank is saved as `null` |
| `userId` | int | yes | `> 0`. Written to `CreatedBy` on add, `Updatedby` on edit. Temporary — from the token once authentication is on |

#### Example request

```json
{ "statusId": 0, "organizationId": 12, "status": "In Progress", "colorCode": "#1E88E5", "userId": 7 }
```

#### Success response — `200 OK`

`data` is the saved `TimesheetProjectStatusResponse`:

```json
{
  "success": true,
  "message": "Project status saved.",
  "data": { "statusId": 4, "status": "In Progress", "colorCode": "#1E88E5" },
  "errors": []
}
```

#### Error responses

**400** — every failing field is listed together, e.g.
`Status: Status is required.`, `UserId: UserId is required: the row records who created or last updated it.`

**404** — a `statusId` was sent, but no live status of that organisation has
it: `Project status '4' was not found.`

**500** — unhandled defect, or the procedure has not been created in the
database yet.

---

### 34. Remove Project Status

`POST /api/v1/TimesheetProject/remove-timesheet-project-status`

**Purpose** — Soft-delete a project status. The row stays, stamped with
`DeletedDate` and `Deletedby`, and the status list and Add / Edit Project
Status no longer see it. By the API through EF Core; no stored procedure.

Projects already holding the status keep it, and still show its name.

#### Request body — `TimesheetProjectStatusDeleteRequest` (JSON)

| Field | Type | Required | Rules |
|---|---|---|---|
| `statusId` | int | yes | `> 0` |
| `deletedBy` | int | yes | `> 0`. Written to `Deletedby`. Temporary — from the token once authentication is on |

#### Example request

```json
{ "statusId": 4, "deletedBy": 7 }
```

#### Success response — `200 OK`

```json
{ "success": true, "message": "Project status removed.", "data": null, "errors": [] }
```

#### Error responses

**400** — `statusId` or `deletedBy` `<= 0`; both are listed when both fail.

**404** — no live status has that id. Removing one twice is **not** a silent
success:

```json
{ "success": false, "message": "Project status '4' was not found.", "data": null, "errors": [] }
```

**500** — unhandled defect.

---

### 35. Organisation Project Budget List

`GET /api/v1/TimesheetProject/get-timesheet-project-budget-by-orgID/{orgID}`

**Purpose** — The budget lines of every project of one organisation, each with
its project and category name. Read through
`dbo.spc_GetTimesheetProjectBudgetByOrgID`.

The organisation is reached through the project — `dbo.TimesheetProjectBudget`
has no organisation column. A project with no budget is **not** listed; this is
a list of budget lines, not of projects.

#### Route parameters

| Name | Type | Rules |
|---|---|---|
| `orgID` | int | `> 0` — checked by the service |

#### Example request

```
GET /api/v1/TimesheetProject/get-timesheet-project-budget-by-orgID/12
```

#### Success response — `200 OK`

`data` is an array of `TimesheetProjectBudgetByOrgResponse`, ordered by
`projectName`, then `periodStart`, then `budgetId`:

| Field | Type | Notes |
|---|---|---|
| `projectId` | int | `dbo.TimesheetProject.ProjectID` |
| `projectCode` | string \| null | `dbo.TimesheetProject.Code` — read from the project, so never `null` for an old row |
| `projectName` | string \| null | The `Projectname` column |
| `budgetId` | int | `dbo.TimesheetProjectBudget.BudgetID` |
| `projectCategoryId` | int \| null | |
| `categoryName` | string \| null | From `dbo.TimesheetProjectCategory`; `null` when the category is null, unknown or deleted |
| `allocatedHours` | decimal \| null | A number of hours |
| `hourlyRate` | decimal \| null | The `Hourlyrate` column |
| `allocatedAmount` | decimal \| null | The `Allocated_amount` column — hours × rate, worked out on save |
| `periodStart` | datetime \| null | A date; the time part is always midnight |
| `periodEnd` | datetime \| null | A date; the time part is always midnight |

#### Example response

```json
{
  "success": true,
  "message": "",
  "data": [
    {
      "projectId": 4,
      "projectCode": "PR0004",
      "projectName": "Website Rebuild",
      "budgetId": 8,
      "projectCategoryId": 3,
      "categoryName": "Development",
      "allocatedHours": 120.50,
      "hourlyRate": 45.00,
      "allocatedAmount": 5422.50,
      "periodStart": "2026-10-01T00:00:00",
      "periodEnd": "2026-12-31T00:00:00"
    }
  ],
  "errors": []
}
```

#### Empty result

An organisation with no budget lines — or no such organisation — is
`data: []`, a 200.

#### Error responses

**400** — `orgID <= 0`: `orgID: orgID is required and must be greater than 0.`

**500** — unhandled defect, or the procedure has not been created in the
database yet.

---

### 36. Create Timesheet User

`POST /api/v1/TimesheetProject/create-timesheet-user`

**Purpose** — Sign a user up and put them on a project of their organisation.
Written through `dbo.spc_Signup_timesheet_user`.

| `email` | Result |
|---|---|
| not yet a user | **Creates** the user (`dbo.Signup`, role 2), their `dbo.Login` row, and the project assignment. `isNewUser: true` |
| a live user of `organizationId` | **Adds the project assignment only.** The user is not changed — `fullName`, `countryId` and `address` are ignored. `isNewUser: false` |
| a removed user, or a user of another organisation | **409** — nothing is written |

The email is matched ignoring case and surrounding spaces, and stored upper
case.

**Welcome email.** A **new** account is sent the welcome email
(`WelcomeUserTemplate.html`) straight away, through the email service — nothing
is queued or stored. The login URL, support address and subject come from the
`WelcomeEmail` configuration section. An existing user put on another project
gets no email. **A failed email does not fail the request**: the account is
already created, so the response is still `200`, with `welcomeEmailSent: false`,
and the reason is logged.

#### Request body — `TimesheetUserCreateRequest` (JSON)

| Field | Type | Required | Rules | Notes |
|---|---|---|---|---|
| `fullName` | string | yes | `<= 50` characters after trimming | |
| `email` | string | yes | a valid address, `<= 50` characters | |
| `countryId` | int \| null | no | `> 0`; an existing `dbo.Country` | |
| `address` | string \| null | no | | Blank is saved as `null` |
| `organizationId` | int | yes | `> 0`; an existing organisation | |
| `projectId` | int | yes | `> 0`; a project of `organizationId` | |
| `projectRoleId` | int \| null | no | `> 0`; a live role of `organizationId` | A `roleId` from `get-user-role-list-by-orgid` |
| `jobType` | int \| null | no | `> 0` | Not checked against a list |
| `costRate` | decimal | yes | `>= 0` | Cost per hour |
| `billRate` | decimal | yes | `>= 0` | Billable rate per hour |
| `workLocationRange` | int \| null | no | `>= 0` | |
| `projectLocationLatitude` | string \| null | no | a number from -90 to 90, `<= 25` characters | Saved for a **new** user only |
| `maxLogHoursPerDay` | int \| null | no | `0`–`24` | The most hours a day this assignment may log. **Stored only — nothing enforces it**; see [Known gaps](#known-gaps) 57. Unlike the latitude, it is saved for an existing user too |
| `createdBy` | int | yes | `> 0` | Temporary — from the token once authentication is on |

The procedure's `@Range` and `@Projectloclongtitude` parameters are **not**
exposed: the procedure never uses either.

> `maxLogHoursPerDay` is passed as `@Maxloghourday`, added to
> `spc_Signup_timesheet_user` on 2026-10-18 and **still DRAFT**. Until the
> owner applies the revised procedure, every call to this endpoint fails on
> the unknown parameter and answers **500** — including calls that leave
> `maxLogHoursPerDay` out.

#### Example request

```json
{
  "fullName": "Priya Shah",
  "email": "priya.shah@example.com",
  "countryId": 101,
  "address": "12 MG Road, Pune",
  "organizationId": 12,
  "projectId": 4,
  "projectRoleId": 3,
  "jobType": 1,
  "costRate": 25.00,
  "billRate": 45.00,
  "workLocationRange": 500,
  "projectLocationLatitude": "18.5204",
  "maxLogHoursPerDay": 8,
  "createdBy": 7
}
```

#### Success response — `200 OK`

`data` is a `TimesheetUserCreateResponse` — the user as `dbo.Signup` holds it
after the save. `message` is `User created.` or `User added to the project.`

| Field | Type | Notes |
|---|---|---|
| `userId` | int | `dbo.Signup.UserID` |
| `name` | string \| null | An existing user keeps their own name |
| `email` | string \| null | As stored — upper case |
| `organizationId` | int \| null | |
| `projectId` | int | |
| `isNewUser` | bool | `false` when an existing user was only put on the project |
| `welcomeEmailSent` | bool | Whether the welcome email went out. Always `false` when `isNewUser` is `false` |

```json
{
  "success": true,
  "message": "User created.",
  "data": {
    "userId": 58,
    "name": "Priya Shah",
    "email": "PRIYA.SHAH@EXAMPLE.COM",
    "organizationId": 12,
    "projectId": 4,
    "isNewUser": true,
    "welcomeEmailSent": true
  },
  "errors": []
}
```

#### Error responses

**400 — validation** (every failure listed): a missing or malformed field from
the table above, then — once the shape is right — an organisation, country,
project or role that does not exist, e.g.
`ProjectId: Project '4' does not exist in organization '12'.`

**409** — `The user with email '…' has been removed.` /
`The user with email '…' belongs to another organization.`

**500** — unhandled defect, the procedure has not been created in the database,
or the procedure failed. The procedure swallows its own errors without rolling
back, so the API sees only SQL Server error 266; it rolls the transaction back
and returns the generic 500. The real cause is not reported.

#### Not checked

Putting the same user on the same project twice adds a second assignment row:
`dbo.Timesheet_Users_Project_setup` has no unique constraint, and neither the
procedure nor the API checks for an existing assignment.

---

### 37. Edit Timesheet User

`POST /api/v1/TimesheetProject/edit-timesheet-user`

**Purpose** — Edit one project assignment of a user, and the user's name and
email. Written through EF Core, not a procedure.

| Field | Saved to |
|---|---|
| `fullName`, `email` | The user in `dbo.Signup` — **only when they change**, with `UpdatedDate` / `UpdateBy`. They change for every project the user is on |
| everything else | The assignment in `dbo.Timesheet_Users_Project_setup` |

Both are saved in one transaction — both or neither. **An edit overwrites every
field**: an optional field left out is saved as `null`. Send the whole
assignment.

> Needs `TimesheetUserProjectSetupID` on `dbo.Timesheet_Users_Project_setup`.
> **Applied** — the `sp_help` list supplied on 2026-10-18 shows the column
> present and `NOT NULL`.

#### Request body — `TimesheetUserUpdateRequest` (JSON)

| Field | Type | Required | Rules | Notes |
|---|---|---|---|---|
| `timesheetUserProjectSetupId` | int | yes | `> 0`; a live assignment | |
| `organizationId` | int | yes | `> 0` | The assignment's user must belong to it |
| `fullName` | string | yes | `<= 50` characters after trimming | |
| `email` | string | yes | a valid address, `<= 50` characters; not another user's | Stored upper case. Compared ignoring case, so a case-only change is not a change |
| `projectId` | int | yes | `> 0`; a project of `organizationId`; not one the user is on through another live assignment | May move the assignment to another project |
| `projectRoleId` | int \| null | no | `> 0`; a live role of `organizationId` | |
| `jobType` | int \| null | no | `> 0` | |
| `costRate` | decimal | yes | `>= 0` | |
| `billRate` | decimal | yes | `>= 0` | |
| `workLocationRange` | int \| null | no | `>= 0` | |
| `projectLocationLatitude` | string \| null | no | a number from -90 to 90, `<= 25` characters | |
| `projectLocationLongitude` | string \| null | no | a number from -180 to 180, `<= 25` characters | |
| `maxLogHoursPerDay` | int \| null | no | `0`–`24` | **Stored only — nothing enforces it**; see [Known gaps](#known-gaps) 57 |
| `updatedBy` | int | yes | `> 0` | Temporary — from the token once authentication is on |

#### Example request

```json
{
  "timesheetUserProjectSetupId": 21,
  "organizationId": 12,
  "fullName": "Priya Shah",
  "email": "priya.shah@example.com",
  "projectId": 4,
  "projectRoleId": 3,
  "jobType": 1,
  "costRate": 28.00,
  "billRate": 50.00,
  "workLocationRange": 500,
  "projectLocationLatitude": "18.5204",
  "projectLocationLongitude": "73.8567",
  "maxLogHoursPerDay": 8,
  "updatedBy": 7
}
```

#### Success response — `200 OK`

`data` is a `TimesheetUserResponse`:

```json
{
  "success": true,
  "message": "Timesheet user saved.",
  "data": {
    "timesheetUserProjectSetupId": 21,
    "userId": 58,
    "name": "Priya Shah",
    "email": "PRIYA.SHAH@EXAMPLE.COM",
    "organizationId": 12,
    "projectId": 4,
    "projectRoleId": 3,
    "jobType": 1,
    "costRate": 28.00,
    "billRate": 50.00,
    "workLocationRange": 500,
    "projectLocationLatitude": "18.5204",
    "projectLocationLongitude": "73.8567",
    "maxLogHoursPerDay": 8
  },
  "errors": []
}
```

#### Error responses

**400 — validation** (every failure listed): a missing or malformed field from
the table above, then — once the shape is right — a project or role that does
not exist in the organisation.

**404** — no live assignment has that id, or its user is removed or belongs to
another organisation: `Timesheet user '21' was not found.`

**409** — `The email '…' is already used by another user.` /
`User '58' is already on project '4' through another assignment.`

**500** — unhandled defect.

---

### 38. Remove Timesheet User

`POST /api/v1/TimesheetProject/remove-timesheet-user`

**Purpose** — Take a user off a project, by `userId` and `projectId`. A soft
delete: the user's live assignment to the project in
`dbo.Timesheet_Users_Project_setup` stays, stamped with `IsDelete = 1`,
`DeletedDate` (UTC) and `DeletedBy`. Normally one row; if
`create-timesheet-user` added duplicates, **every** live one is removed. The
user in `dbo.Signup` and their other projects are **not** touched.

> Needs `TimesheetUserProjectSetupID` on `dbo.Timesheet_Users_Project_setup`.
> **Applied** — the `sp_help` list supplied on 2026-10-18 shows the column
> present and `NOT NULL`. EF Core updates rows by that key.

#### Request body — `TimesheetUserDeleteRequest` (JSON)

| Field | Type | Required | Rules |
|---|---|---|---|
| `userId` | int | yes | `> 0` — a `dbo.Signup.UserID` |
| `projectId` | int | yes | `> 0` |
| `deletedBy` | int | yes | `> 0` — temporary, from the token once authentication is on |

```json
{ "userId": 58, "projectId": 4, "deletedBy": 7 }
```

#### Success response — `200 OK`

```json
{ "success": true, "message": "Timesheet user removed.", "data": null, "errors": [] }
```

#### Error responses

**400** (every failure listed) — `UserId is required and must be greater than 0.` /
`ProjectId is required and must be greater than 0.` /
`DeletedBy is required: a soft delete records who performed it.`

**404** — the user has no live assignment to the project — including one
removed already: `User '58' is not on project '4'.`

**500** — unhandled defect, or the key column has not been added yet.

---

### 39. Organisation Timesheet User List

`GET /api/v1/TimesheetProject/get-timesheet-user-by-orgID/{orgID}`

**Purpose** — The timesheet users of one organisation: every live project
assignment in `dbo.Timesheet_Users_Project_setup` of every live user in
`dbo.Signup` of the organisation, with the user's name and email, the
project's code and name, and the project role's name. Read through
`dbo.spc_GetTimesheetUserListByOrgID`.

One row per **assignment**, not per user: a user on three projects is three
rows, and a user with no live assignment is **not** listed. The organisation is
the user's (`dbo.Signup.OrganizationID`).

#### Route parameters

| Name | Type | Rules |
|---|---|---|
| `orgID` | int | `> 0` — checked by the service |

#### Example request

```
GET /api/v1/TimesheetProject/get-timesheet-user-by-orgID/12
```

#### Success response — `200 OK`

`data` is an array of `TimesheetUserListResponse`, ordered by `name`, then
`projectName`, then `timesheetUserProjectSetupId`:

| Field | Type | Notes |
|---|---|---|
| `timesheetUserProjectSetupId` | int | The assignment — the id `edit-timesheet-user` takes |
| `userId` | int | `dbo.Signup.UserID` |
| `name` | string \| null | |
| `email` | string \| null | As stored: upper case |
| `organizationId` | int \| null | |
| `projectId` | int \| null | |
| `projectCode` | string \| null | `dbo.TimesheetProject.Code`; `null` when the project is unknown |
| `projectName` | string \| null | The `Projectname` column; `null` when the project is unknown |
| `projectRoleId` | int \| null | |
| `projectRoleName` | string \| null | From `dbo.TimesheetUserRole`; `null` when the role is null, unknown or deleted |
| `jobType` | int \| null | |
| `costRate` | decimal \| null | The `Cost_rate_per_hour` column |
| `billRate` | decimal \| null | The `Billable_rate_per_hour` column |
| `workLocationRange` | int \| null | |
| `projectLocationLatitude` | string \| null | Text, as stored |
| `projectLocationLongitude` | string \| null | Text, as stored |
| `maxLogHoursPerDay` | int \| null | The most hours a day this assignment may log; `null` when it sets no cap. **Stored only — nothing enforces it** |

#### Example response

```json
{
  "success": true,
  "message": "",
  "data": [
    {
      "timesheetUserProjectSetupId": 31,
      "userId": 58,
      "name": "Asha Patel",
      "email": "ASHA.PATEL@EXAMPLE.COM",
      "organizationId": 12,
      "projectId": 4,
      "projectCode": "PR0004",
      "projectName": "Website Rebuild",
      "projectRoleId": 3,
      "projectRoleName": "Developer",
      "jobType": 1,
      "costRate": 30.00,
      "billRate": 45.00,
      "workLocationRange": 200,
      "projectLocationLatitude": "23.0225",
      "projectLocationLongitude": null,
      "maxLogHoursPerDay": 8
    }
  ],
  "errors": []
}
```

#### Empty result

An organisation with no timesheet users — or no such organisation — is
`data: []`, a 200.

#### Error responses

**400** — `orgID <= 0`: `orgID: orgID is required and must be greater than 0.`

**500** — unhandled defect, or the procedure has not been created in the
database yet.

---

### 40. Holiday List

`GET /api/v1/TimesheetProject/get-timesheet-holiday-setup?orgID={orgID}&projectID={projectID}`

**Purpose** — The live holidays in `dbo.TimesheetHolidaysetup`, filtered by
organisation, by project, or both, each with its country, client and project
names. Read through `dbo.spc_GetTimesheetHolidaySetup`.

#### Query parameters

| Name | Type | Rules |
|---|---|---|
| `orgID` | int \| absent | `>= 0`. Absent or `0` = not filtered by organisation |
| `projectID` | int \| absent | `>= 0`. Absent or `0` = not filtered by project |

| Sent | Returns |
|---|---|
| `orgID` only | Every holiday of the organisation, whatever its project (including none) |
| `projectID` only | Every holiday of the project, whatever its organisation |
| both | The project's holidays within the organisation |
| neither | **400** — the whole table is never returned |

#### Example request

```
GET /api/v1/TimesheetProject/get-timesheet-holiday-setup?orgID=12
```

#### Success response — `200 OK`

`data` is an array of `TimesheetHolidaySetupResponse`, ordered by
`holidayDate`, then `timesheetHolidaySetupId`. Deleted holidays
(`IsDelete = 1`) are not included.

| Field | Type | Notes |
|---|---|---|
| `timesheetHolidaySetupId` | int | The id the edit and remove take |
| `holidayDate` | datetime \| null | A `date` column — the time part is always midnight |
| `description` | string \| null | The holiday's name |
| `organizationId` | int \| null | |
| `countryId` | int \| null | |
| `countryName` | string \| null | `dbo.Country.Name`; `null` when no or an unknown country |
| `clientId` | int \| null | |
| `clientName` | string \| null | The `Clientname` column; `null` when no or an unknown client |
| `projectId` | int \| null | |
| `projectCode` | string \| null | `dbo.TimesheetProject.Code`; `null` when no or an unknown project |
| `projectName` | string \| null | The `Projectname` column |
| `createDate` | datetime \| null | |
| `createdBy` | int \| null | |

#### Example response

```json
{
  "success": true,
  "message": "",
  "data": [
    {
      "timesheetHolidaySetupId": 5,
      "holidayDate": "2026-12-25T00:00:00",
      "description": "Christmas Day",
      "organizationId": 12,
      "countryId": 1,
      "countryName": "India",
      "clientId": 3,
      "clientName": "Acme Corp",
      "projectId": 4,
      "projectCode": "PR0004",
      "projectName": "Website Rebuild",
      "createDate": "2026-10-17T09:12:44.317",
      "createdBy": 7
    }
  ],
  "errors": []
}
```

#### Empty result

No matching holidays is `data: []`, a 200.

#### Error responses

**400** — neither filter sent (`orgID: orgID or projectID is required.`), or
one is negative (`orgID: orgID must be greater than 0, or left out.`).

**500** — unhandled defect, or the procedure has not been created in the
database yet.

---

### 41. Add / Edit Holiday

`POST /api/v1/TimesheetProject/add-edit-timesheet-holiday-setup`

**Purpose** — Add a holiday to an organisation, or edit one — **one endpoint
for both**. `timesheetHolidaySetupId` `0` (or absent) adds; an existing id
edits that holiday, which must belong to `organizationId` and be live. An edit
overwrites `holidayDate`, `description`, `countryId`, `clientId` and
`projectId`, so send them all. Written through
`dbo.spc_AddEditTimesheetHolidaySetup`.

#### Request body — `TimesheetHolidaySetupSaveRequest` (JSON)

| Field | Type | Required | Rules |
|---|---|---|---|
| `timesheetHolidaySetupId` | int \| null | no | `0` / absent to add; `> 0` to edit |
| `holidayDate` | date | yes | Any time part is ignored |
| `description` | string \| null | no | At most 100 characters; trimmed. Blank is saved as `null` |
| `organizationId` | int | yes | `> 0` |
| `countryId` | int \| null | no | `> 0`; must be a `dbo.Country` id |
| `clientId` | int \| null | no | `> 0`; must be a live client of `organizationId` |
| `projectId` | int \| null | no | `> 0`; must be a project of `organizationId` |
| `createdBy` | int | yes | `> 0`. Written to `CreatedBy` on add only — the table has no "updated by" column. Temporary — from the token once authentication is on |

#### Example request

```json
{
  "timesheetHolidaySetupId": 0,
  "holidayDate": "2026-12-25",
  "description": "Christmas Day",
  "organizationId": 12,
  "countryId": 1,
  "clientId": 3,
  "projectId": 4,
  "createdBy": 7
}
```

#### Success response — `200 OK`

`data` is the saved holiday, in the same shape as [Holiday List](#40-holiday-list):

```json
{
  "success": true,
  "message": "Holiday saved.",
  "data": {
    "timesheetHolidaySetupId": 5,
    "holidayDate": "2026-12-25T00:00:00",
    "description": "Christmas Day",
    "organizationId": 12,
    "countryId": 1,
    "countryName": "India",
    "clientId": 3,
    "clientName": "Acme Corp",
    "projectId": 4,
    "projectCode": "PR0004",
    "projectName": "Website Rebuild",
    "createDate": "2026-10-17T09:12:44.317",
    "createdBy": 7
  },
  "errors": []
}
```

#### Error responses

**400** — every failing field is listed together. Field rules first
(`HolidayDate: HolidayDate is required.`,
`Description: Description must be 100 characters or fewer.`), then the
database checks (`Country '99' does not exist.`,
`Client '3' does not exist in organization '12' or has been removed.`,
`Project '4' does not exist in organization '12'.`).

**404** — a `timesheetHolidaySetupId` was sent, but no live holiday of that
organisation has it: `Holiday '5' was not found.`

**500** — unhandled defect, or the procedure has not been created in the
database yet.

---

### 42. Remove Holiday

`POST /api/v1/TimesheetProject/remove-timesheet-holiday-setup`

**Purpose** — Soft-delete a holiday of an organisation. The row stays with
`IsDelete = 1`, and Holiday List and Add / Edit Holiday no longer see it. The
table has no delete date or "deleted by" column, so nothing else is recorded.
By the API through EF Core; no stored procedure.

#### Request body — `TimesheetHolidaySetupDeleteRequest` (JSON)

| Field | Type | Required | Rules |
|---|---|---|---|
| `timesheetHolidaySetupId` | int | yes | `> 0` |
| `organizationId` | int | yes | `> 0`; the holiday must belong to it |

#### Example request

```json
{ "timesheetHolidaySetupId": 5, "organizationId": 12 }
```

#### Success response — `200 OK`

```json
{ "success": true, "message": "Holiday removed.", "data": null, "errors": [] }
```

#### Error responses

**400** — `timesheetHolidaySetupId` or `organizationId` `<= 0`; both are
listed when both fail.

**404** — no live holiday with that id belongs to that organisation. Removing
one twice is **not** a silent success: `Holiday '5' was not found.`

**500** — unhandled defect.

---

### 43. Weekend Setup List

`GET /api/v1/TimesheetProject/get-timesheet-weekend-setup?orgID={orgID}&projectID={projectID}`

**Purpose** — The live weekend setups in `dbo.TimeSheet_Weekend_setup` —
which days of the week are weekend days for each project — with the client and
project names. Read through `dbo.spc_GetTimesheetWeekendSetup`.

#### Query parameters

| Name | Type | Rules |
|---|---|---|
| `orgID` | int \| absent | `>= 0`. Absent or `0` = not filtered by organisation |
| `projectID` | int \| absent | `>= 0`. Absent or `0` = not filtered by project |

| Sent | Returns |
|---|---|
| `orgID` only | Every setup of the organisation |
| `projectID` only | The project's setup — at most one |
| both | The project's setup within the organisation |
| neither | **400** — the whole table is never returned |

#### Example request

```
GET /api/v1/TimesheetProject/get-timesheet-weekend-setup?projectID=4
```

#### Success response — `200 OK`

`data` is an array of `TimesheetWeekendSetupResponse`, ordered by project
name, then `timeSheetWeekendSetupId`. Deleted setups are not included.

| Field | Type | Notes |
|---|---|---|
| `timeSheetWeekendSetupId` | int | The id the edit and remove take |
| `organizationId` | int | The `OrgID` column |
| `clientId` | int \| null | |
| `clientName` | string \| null | `null` when no or an unknown client |
| `projectId` | int \| null | |
| `projectCode` | string \| null | |
| `projectName` | string \| null | |
| `isMondayWeekend` … `isSundayWeekend` | bool \| null | Seven fields, Monday to Sunday — `true` when that day is a weekend day |
| `createDate` | datetime \| null | |
| `createdBy` | int \| null | |
| `updatedDate` | datetime \| null | `null` until the first edit |
| `updatedBy` | int \| null | |

#### Example response

```json
{
  "success": true,
  "message": "",
  "data": [
    {
      "timeSheetWeekendSetupId": 2,
      "organizationId": 12,
      "clientId": 3,
      "clientName": "Acme Corp",
      "projectId": 4,
      "projectCode": "PR0004",
      "projectName": "Website Rebuild",
      "isMondayWeekend": false,
      "isTuesdayWeekend": false,
      "isWednesdayWeekend": false,
      "isThursdayWeekend": false,
      "isFridayWeekend": false,
      "isSaturdayWeekend": true,
      "isSundayWeekend": true,
      "createDate": "2026-10-18T08:02:11.510",
      "createdBy": 7,
      "updatedDate": null,
      "updatedBy": null
    }
  ],
  "errors": []
}
```

#### Empty result

No matching setups is `data: []`, a 200.

#### Error responses

**400** — neither filter sent (`orgID: orgID or projectID is required.`), or
one is negative.

**500** — unhandled defect, or the procedure has not been created in the
database yet.

---

### 44. Add / Edit Weekend Setup

`POST /api/v1/TimesheetProject/add-edit-timesheet-weekend-setup`

**Purpose** — Add the weekend setup of a project, or edit one — **one endpoint
for both**. `timeSheetWeekendSetupId` `0` (or absent) adds; an existing id
edits that setup, which must belong to `organizationId` and be live. An edit
overwrites the client, the project and all seven day flags, so send them all.
Written through `dbo.spc_AddEditTimesheetWeekendSetup`.

**A project and its weekend setup are one-to-one:**

| Rule | How the API keeps it |
|---|---|
| One setup → one project | `projectId` is required, and the table holds a single `ProjectID` per row |
| One project → one setup | A second live setup for the same project — by an add, or by an edit moving a setup onto it — is a **409**. The procedure checks again under a lock, so two saves cannot both add |

Neither rule is enforced by the table — see [Known gaps](#known-gaps) 51.

#### Request body — `TimesheetWeekendSetupSaveRequest` (JSON)

| Field | Type | Required | Rules |
|---|---|---|---|
| `timeSheetWeekendSetupId` | int \| null | no | `0` / absent to add; `> 0` to edit |
| `organizationId` | int | yes | `> 0` |
| `clientId` | int \| null | no | `> 0`; must be a live client of `organizationId` |
| `projectId` | int | yes | `> 0`; must be a project of `organizationId` with no other live setup |
| `isMondayWeekend` … `isSundayWeekend` | bool | no | Seven fields. Absent is `false` |
| `userId` | int | yes | `> 0`. Written to `CreatedBy` on add, `UpdatedBy` on edit. Temporary — from the token once authentication is on |

#### Example request

```json
{
  "timeSheetWeekendSetupId": 0,
  "organizationId": 12,
  "clientId": 3,
  "projectId": 4,
  "isMondayWeekend": false,
  "isTuesdayWeekend": false,
  "isWednesdayWeekend": false,
  "isThursdayWeekend": false,
  "isFridayWeekend": false,
  "isSaturdayWeekend": true,
  "isSundayWeekend": true,
  "userId": 7
}
```

#### Success response — `200 OK`

`data` is the saved setup, in the same shape as
[Weekend Setup List](#43-weekend-setup-list), with
`"message": "Weekend setup saved."`.

#### Error responses

**400** — every failing field is listed together, e.g.
`ProjectId: ProjectId is required and must be greater than 0.`,
`Project '4' does not exist in organization '12'.`

**404** — a `timeSheetWeekendSetupId` was sent, but no live setup of that
organisation has it: `Weekend setup '2' was not found.`

**409** — the project already has another live setup:

```json
{ "success": false, "message": "Project '4' already has a weekend setup. Edit that one instead.", "data": null, "errors": [] }
```

**500** — unhandled defect, or the procedure has not been created in the
database yet. Also the loser of two saves for the same project racing past the
409 check — see [Known gaps](#known-gaps) 51.

---

### 45. Remove Weekend Setup

`POST /api/v1/TimesheetProject/remove-timesheet-weekend-setup`

**Purpose** — Soft-delete a weekend setup by its id. The row stays, stamped
with `IsDelete = 1`, `DeletedDate` and `DeletedBy`; the project may then be
given a new setup. By the API through EF Core; no stored procedure.

#### Request body — `TimesheetWeekendSetupDeleteRequest` (JSON)

| Field | Type | Required | Rules |
|---|---|---|---|
| `timeSheetWeekendSetupId` | int | yes | `> 0` |
| `deletedBy` | int | yes | `> 0`. Written to `DeletedBy`. Temporary — from the token once authentication is on |

#### Example request

```json
{ "timeSheetWeekendSetupId": 2, "deletedBy": 7 }
```

#### Success response — `200 OK`

```json
{ "success": true, "message": "Weekend setup removed.", "data": null, "errors": [] }
```

#### Error responses

**400** — `timeSheetWeekendSetupId` or `deletedBy` `<= 0`; both are listed
when both fail.

**404** — no live setup has that id. Removing one twice is **not** a silent
success: `Weekend setup '2' was not found.`

**500** — unhandled defect.

---

### 46. User Access Setup List

`GET /api/v1/TimesheetProject/get-user-access-setup?orgID={orgID}&userID={userID}`

**Purpose** — The user access setups in `dbo.Timesheet_role_assignment` —
which modules each user may use — with the user's name, email and
organisation. Read through `dbo.spc_GetUserAccessSetup`.

The table has no organisation column; the organisation is the **user's**
(`dbo.Signup.OrganizationID`). Setups of removed users (`dbo.Signup.IsDelete`
other than `0`) are not listed.

#### Query parameters

| Name | Type | Rules |
|---|---|---|
| `orgID` | int \| absent | `>= 0`. Absent or `0` = not filtered by organisation |
| `userID` | int \| absent | `>= 0`. Absent or `0` = not filtered by user |

| Sent | Returns |
|---|---|
| `orgID` only | Every setup of the organisation's live users |
| `userID` only | The user's setup — at most one |
| both | The user's setup, if the user is in the organisation |
| neither | **400** — the whole table is never returned |

#### Example request

```
GET /api/v1/TimesheetProject/get-user-access-setup?orgID=12
```

#### Success response — `200 OK`

`data` is an array of `TimesheetRoleAssignmentResponse`, ordered by user name,
then `assignmentId`.

| Field | Type | Notes |
|---|---|---|
| `assignmentId` | int | The id the edit and remove take |
| `userId` | int \| null | |
| `userName` | string \| null | `dbo.Signup.Name` |
| `email` | string \| null | As stored |
| `organizationId` | int \| null | The user's organisation |
| `isClientSetup` | bool \| null | The `Mod_IsclientSetup` column |
| `isProject` | bool \| null | `Mod_Isproject` |
| `isProjectBudget` | bool \| null | `Mod_IsProjectbudget` |
| `isProjectCategory` | bool \| null | `Mod_IsProjectCategory` |
| `isStatusSetup` | bool \| null | `Mod_IsStatusSetup` |
| `isRoleSetup` | bool \| null | `Mod_IsRoleSetup` |
| `isUserSetup` | bool \| null | `Mod_IsUsersetup` |
| `isTimeLog` | bool \| null | `Mod_IsTimeLog` |
| `isViewTimesheet` | bool \| null | `Mod_IsViewTimesheet` |
| `createDate` | datetime \| null | |
| `createdBy` | int \| null | |
| `updatedDate` | datetime \| null | `null` until the first edit |
| `updatedBy` | int \| null | The `Updatedby` column |

#### Example response

```json
{
  "success": true,
  "message": "",
  "data": [
    {
      "assignmentId": 2,
      "userId": 58,
      "userName": "Asha Patel",
      "email": "ASHA.PATEL@EXAMPLE.COM",
      "organizationId": 12,
      "isClientSetup": false,
      "isProject": true,
      "isProjectBudget": true,
      "isProjectCategory": false,
      "isStatusSetup": false,
      "isRoleSetup": false,
      "isUserSetup": false,
      "isTimeLog": true,
      "isViewTimesheet": true,
      "createDate": "2026-10-18T10:41:07.233",
      "createdBy": 7,
      "updatedDate": null,
      "updatedBy": null
    }
  ],
  "errors": []
}
```

#### Empty result

No matching setups is `data: []`, a 200.

#### Error responses

**400** — neither filter sent (`orgID: orgID or userID is required.`), or one
is negative.

**500** — unhandled defect, or the procedure has not been created in the
database yet.

---

### 47. Add / Edit User Access Setup

`POST /api/v1/TimesheetProject/add-edit-user-access-setup`

**Purpose** — Add the access setup of a user, or edit one — **one endpoint for
both**. `assignmentId` `0` (or absent) adds; an existing `assignmentId` edits
it. An edit overwrites the user and all nine module flags, so send them all.
Written through `dbo.spc_AddEditUserAccessSetup`.

**One user has only one setup.** Adding a second setup for a user, or editing
a setup onto a user who already has one, is a **409**; the procedure checks
again under a lock. The table does not enforce it — see
[Known gaps](#known-gaps) 54.

#### Request body — `TimesheetRoleAssignmentSaveRequest` (JSON)

| Field | Type | Required | Rules |
|---|---|---|---|
| `assignmentId` | int \| null | no | `0` / absent to add; `> 0` to edit |
| `organizationId` | int | yes | `> 0`. **Not stored** — the table has no organisation column — but the user must be a live user of it |
| `userId` | int | yes | `> 0`; a live user (`dbo.Signup.IsDelete = 0`) of `organizationId` with no other setup |
| `isClientSetup`, `isProject`, `isProjectBudget`, `isProjectCategory`, `isStatusSetup`, `isRoleSetup`, `isUserSetup`, `isTimeLog`, `isViewTimesheet` | bool | no | Absent is `false` |
| `loginUserId` | int | yes | `> 0`. The user performing the save — written to `CreatedBy` on add, `Updatedby` on edit. Temporary — from the token once authentication is on |

#### Example request

```json
{
  "assignmentId": 0,
  "organizationId": 12,
  "userId": 58,
  "isClientSetup": false,
  "isProject": true,
  "isProjectBudget": true,
  "isProjectCategory": false,
  "isStatusSetup": false,
  "isRoleSetup": false,
  "isUserSetup": false,
  "isTimeLog": true,
  "isViewTimesheet": true,
  "loginUserId": 7
}
```

#### Success response — `200 OK`

`data` is the saved setup, in the same shape as
[User Access Setup List](#46-user-access-setup-list), with
`"message": "User access setup saved."`.

#### Error responses

**400** — field rules, every failure listed together
(`UserId: UserId is required and must be greater than 0.`,
`LoginUserId: LoginUserId is required: the row records who created or last updated it.`);
then the user check:
`UserId: User '58' is not a live user of organization '12'.`

**404** — an `assignmentId` was sent, but no setup has it:
`User access setup '2' was not found.`

**409** — the user already has another setup:

```json
{ "success": false, "message": "User '58' already has a user access setup. Edit that one instead.", "data": null, "errors": [] }
```

**500** — unhandled defect, or the procedure has not been created in the
database yet. Also the loser of two saves for the same user racing past the
409 check.

---

### 48. Remove User Access Setup

`POST /api/v1/TimesheetProject/remove-user-access-setup`

**Purpose** — Delete a user access setup by its `assignmentId`. **A hard
delete**: `dbo.Timesheet_role_assignment` has no soft-delete column, so the
row is physically removed and the user may then be given a new setup. By the
API through EF Core; no stored procedure.

#### Request body — `TimesheetRoleAssignmentDeleteRequest` (JSON)

| Field | Type | Required | Rules |
|---|---|---|---|
| `assignmentId` | int | yes | `> 0` |

#### Example request

```json
{ "assignmentId": 2 }
```

#### Success response — `200 OK`

```json
{ "success": true, "message": "User access setup removed.", "data": null, "errors": [] }
```

#### Error responses

**400** — `assignmentId <= 0`.

**404** — no setup has that id. Removing one twice is **not** a silent
success: `User access setup '2' was not found.`

**500** — unhandled defect.

---

### Project timesheets: periods and sheet codes

Endpoints 49–54 share one model. Read this once; the sections below refer to it.

**A sheet** is one user's timesheet for one project over one **submission
period**, a row in `dbo.TimesheetProjectSubmission`. Time entries
(`dbo.TimesheetProjectTimeLog`) belong to a sheet. A sheet with no entries is
valid: a user can submit a period they logged nothing in.

**Periods** come from the project's `StartDate` and `SubmissionFrequency`, and
are never sent:

| `SubmissionFrequency` | Periods |
|---|---|
| `1` Weekly | 7-day blocks counted from the project's start date |
| `2` Every 15 days | 15-day blocks counted from the project's start date |
| `3` Monthly | Calendar months from the start **day**: a project starting on the 10th has periods from the 10th to the 9th. Each period is counted from the start date itself, so a start on the 31st does not drift after February |

Both period dates are inclusive. A project with no `StartDate` or no
`SubmissionFrequency` has no periods, so time can be neither logged nor
submitted on it.

**Sheet codes** run `PSHT0001`, `PSHT0002` … across the whole table. The next
code is the highest existing one plus 1, including codes of deleted sheets.
After `PSHT9999` the number simply grows a digit, to `PSHT10000`. A code is
never sent in by the client except to name a sheet at submit.

| How a sheet gets a row | When |
|---|---|
| The first time entry in a period | [Add / Edit Project Time Log](#50-add--edit-project-time-log) creates the sheet as Pending with the next code; every later entry in that period shares it |
| A submit of a period with no row | [Submit Project Timesheet](#53-submit-project-timesheet) creates the sheet and submits it with zero time |

**Calculated sheets.** [Project Timesheet List](#52-project-timesheet-list)
also lists every period, up to the current one, that has no row yet, with
`isCalculated: true` and the code it is **predicted** to get. Predictions are
numbered across the whole organisation, oldest period first, then project
name, then user. That way a sheet shows the same predicted code whichever
filter lists it, and no two users are shown the same one. **A predicted code
is not reserved.** When anyone in the organisation opens a sheet, every
prediction after it moves on by one. See [Known gaps](#known-gaps) 58.

**Statuses** — see [`TimesheetProjectSubmissionStatus`](#timesheetprojectsubmissionstatus--the-statusid-column-of-dbotimesheetprojectsubmission):

```
Pending (0) ──submit──▶ Submitted (1) ──approve──▶ Approved (2)   (final)
                              │
                              └────reject───▶ Rejected (3) ──submit──▶ Submitted (1)
```

Time on a **Submitted** or **Approved** sheet cannot be added, edited, moved or
removed. A **Rejected** sheet is open again. Every submit, approve and reject
writes one row to `dbo.TimesheetProjectSubmissionAuditLog`, in the same save as
the change.

**`userId` and `addedBy`.** On the writes, `userId` says **whose** entry or
sheet it is and is checked against it. `addedBy` says **who is acting** and is
written to `CreatedBy` / `UpdatedBy` / `DeletedBy` / `SubmittedBy` /
`ApprovedBy` / `RejectedBy` and the audit log. Both are temporary and come from
the token once authentication is on.

---

### 49. Project Time Log List

`GET /api/v1/TimesheetProject/get-timesheet-project-time-log?userID={userID}&projectID={projectID}&fromDate={fromDate}&toDate={toDate}`

**Purpose** — A user's time entries on projects, from
`dbo.TimesheetProjectTimeLog`, each with the sheet it is on from
`dbo.TimesheetProjectSubmission`. Read by the API through EF Core; no stored
procedure.

#### Query parameters

| Name | Type | Rules |
|---|---|---|
| `userID` | int | **Required**, `> 0` |
| `projectID` | int \| absent | `>= 0`. Absent or `0` = every project |
| `fromDate` | date \| absent | First log date to include |
| `toDate` | date \| absent | Last log date to include; not before `fromDate` |

#### Example request

```
GET /api/v1/TimesheetProject/get-timesheet-project-time-log?userID=7&projectID=12&fromDate=2026-10-13&toDate=2026-10-19
```

#### Success response — `200 OK`

`data` is an array of `TimesheetProjectTimeLogResponse`, ordered by
`logDate`, then `startTime`. Deleted entries, and entries on deleted sheets,
are not included.

| Field | Type | Notes |
|---|---|---|
| `timesheetProjectTimeLogId` | int | The id the edit and remove take |
| `timesheetProjectSubmissionId` | int | The sheet the entry is on |
| `sheetSubmissionCode` | string | The sheet's code, e.g. `PSHT0004` |
| `submissionStatusId` | int | The sheet's status — 0 Pending, 1 Submitted, 2 Approved, 3 Rejected |
| `periodStartDate` | datetime | First day of the sheet's period |
| `periodEndDate` | datetime | Last day of the sheet's period, inclusive |
| `userId` | int | |
| `projectId` | int | |
| `logDate` | datetime | The day worked; time part always `00:00:00` |
| `startTime` | string \| null | `hh:mm:ss`, 24-hour clock |
| `endTime` | string \| null | `hh:mm:ss`, 24-hour clock |
| `durationMinutes` | int | Worked out from the two times |
| `durationHours` | decimal | `durationMinutes` / 60, rounded to 2 places |
| `description` | string \| null | |
| `status` | int | 1 Save, 2 Draft — see [`TimesheetProjectTimeLogStatus`](#timesheetprojecttimelogstatus--the-status-column-of-dbotimesheetprojecttimelog) |
| `createDate` | datetime | UTC |
| `createdBy` | int | |
| `updateDate` | datetime \| null | `null` until the first edit |
| `updatedBy` | int \| null | |

#### Example response

```json
{
  "success": true,
  "message": "",
  "data": [
    {
      "timesheetProjectTimeLogId": 15,
      "timesheetProjectSubmissionId": 4,
      "sheetSubmissionCode": "PSHT0004",
      "submissionStatusId": 0,
      "periodStartDate": "2026-10-13T00:00:00",
      "periodEndDate": "2026-10-19T00:00:00",
      "userId": 7,
      "projectId": 12,
      "logDate": "2026-10-14T00:00:00",
      "startTime": "13:00:00",
      "endTime": "14:30:00",
      "durationMinutes": 90,
      "durationHours": 1.5,
      "description": "Sprint planning",
      "status": 1,
      "createDate": "2026-10-14T09:12:40.117",
      "createdBy": 7,
      "updateDate": null,
      "updatedBy": null
    }
  ],
  "errors": []
}
```

#### Empty result

No matching entries is `data: []`, a 200.

#### Error responses

**400** — `userID` missing or `<= 0` (`userID: userID is required and must be
greater than 0.`), `projectID` negative, or `toDate` before `fromDate`.

**500** — unhandled defect, or the tables have not been created in the
database yet.

---

### 50. Add / Edit Project Time Log

`POST /api/v1/TimesheetProject/add-edit-timesheet-project-time-log`

**Purpose** — Log time on a project, or edit an entry — **one endpoint for
both**. `timesheetProjectTimeLogId` `0` (or absent) adds; an existing id edits
that entry, which must be `userId`'s. An edit overwrites every field, so send
the whole entry. Written by the API through EF Core; no stored procedure.

No sheet code, period or duration is sent:

- The entry goes on `userId`'s sheet for the period of `projectId` that
  contains `logDate` — see
  [periods and sheet codes](#project-timesheets-periods-and-sheet-codes). The
  period's first entry creates that sheet with the next code.
- `durationMinutes` is worked out as the whole minutes from `startTime` to
  `endTime`. Seconds short of a minute are dropped: `13:00:00`–`14:30:45` is 90.
- The sheet's `TotalMinutesLogged` and `TotalHoursLogged` are recalculated in
  the same save as the entry.
- An edit that moves the entry to another period or project moves it to that
  sheet, and both sheets' totals are recalculated.

#### Request body — `TimesheetProjectTimeLogSaveRequest` (JSON)

| Field | Type | Required | Rules |
|---|---|---|---|
| `timesheetProjectTimeLogId` | int | no | `0` / absent to add; `> 0` to edit a live entry of `userId` |
| `userId` | int | yes | `> 0`. Whose time it is; must be assigned to `projectId` |
| `projectId` | int | yes | `> 0`. Must exist and have a `StartDate`, a `SubmissionFrequency` and an organisation |
| `logDate` | date | yes | Only the date part is used. Not before the project's `StartDate`, not after its `EndDate` |
| `startTime` | string | yes | `hh:mm:ss`, **24-hour clock**: `00:00:00` to `23:59:59`. No AM/PM, no `9:00`, no `24:00:00` |
| `endTime` | string | yes | `hh:mm:ss`, 24-hour clock, on the same day and at least one minute after `startTime` |
| `description` | string \| null | no | Free text, no length limit |
| `status` | int | yes | `1` Save or `2` Draft |
| `addedBy` | int | yes | `> 0`. Written to `CreatedBy` on add, `UpdatedBy` on edit. Temporary — from the token once authentication is on |

#### Example request

```json
{
  "timesheetProjectTimeLogId": 0,
  "userId": 7,
  "projectId": 12,
  "logDate": "2026-10-14",
  "startTime": "13:00:00",
  "endTime": "14:30:00",
  "description": "Sprint planning",
  "status": 1,
  "addedBy": 7
}
```

#### Success response — `200 OK`

`data` is the saved entry, in the same shape as
[Project Time Log List](#49-project-time-log-list) — including the sheet's id,
code and period — with `"message": "Time log saved."`.

#### Error responses

**400** — validation; every failing payload field is listed together, e.g.
`StartTime: StartTime is required, as a time of day in hh:mm:ss format - for example "09:00:00".`,
`EndTime: EndTime must be after StartTime, on the same day.`,
`Status: Status must be 1 (Save) or 2 (Draft).`,
`AddedBy: AddedBy is required: the entry records who created or last updated it.`

Also a 400, one at a time, after the payload passes:

| Cause | Message |
|---|---|
| No such project | `ProjectId: Project '12' does not exist.` |
| Project not set up | `Project '12' needs a start date, a submission frequency and an organization before time can be logged or submitted on it.` |
| Date outside the project | `LogDate: LogDate must not be before the project's start date, 2026-10-01.` (or after its end date) |
| User not on the project | `ProjectId: User '7' is not assigned to project '12'.` |
| Entry is another user's | `Time log '15' does not belong to user '7', so it cannot be changed as theirs.` |
| Sheet locked — the entry's current sheet, or the one it would move to | `Timesheet 'PSHT0004' has already been submitted, so its time can no longer be added to or changed.` |

**404** — a `timesheetProjectTimeLogId` was sent, but no live entry has it:
`Time log '15' was not found.`

**500** — unhandled defect, or the tables have not been created in the
database yet.

---

### 51. Remove Project Time Log

`POST /api/v1/TimesheetProject/remove-timesheet-project-time-log`

**Purpose** — Soft-delete a time entry by its id. The row stays, stamped with
`IsDelete = 1`, `DeletedDate` and `DeletedBy`. Its sheet's totals drop by its
minutes in the same save. The sheet itself stays, even when it is left empty:
an empty Pending sheet is still owed. By the API through EF Core; no stored
procedure.

#### Request body — `TimesheetProjectTimeLogDeleteRequest` (JSON)

| Field | Type | Required | Rules |
|---|---|---|---|
| `timesheetProjectTimeLogId` | int | yes | `> 0` |
| `userId` | int | yes | `> 0`. The entry's owner — checked against the entry's sheet |
| `addedBy` | int | yes | `> 0`. Written to `DeletedBy`. Temporary — from the token once authentication is on |

#### Example request

```json
{ "timesheetProjectTimeLogId": 15, "userId": 7, "addedBy": 7 }
```

#### Success response — `200 OK`

```json
{ "success": true, "message": "Time log removed.", "data": null, "errors": [] }
```

#### Error responses

**400** — any id `<= 0` (every failure is listed); the entry is another
user's; or its sheet is Submitted or Approved:
`Timesheet 'PSHT0004' has already been submitted, so its time can no longer be added to or changed.`

**404** — no live entry has that id. Removing one twice is **not** a silent
success: `Time log '15' was not found.`

**500** — unhandled defect.

---

### 52. Project Timesheet List

`GET /api/v1/TimesheetProject/get-timesheet-project-sheet?userID={userID}&projectID={projectID}&orgID={orgID}`

**Purpose** — Project timesheets, one per user, project and period, with the
time on each. This is the list the UI submits from, and the one an approver
reviews. Read by the API through EF Core; no stored procedure.

Two kinds of row — see
[periods and sheet codes](#project-timesheets-periods-and-sheet-codes):

| Kind | Which periods | Code, count and time |
|---|---|---|
| **Stored** (`isCalculated: false`) | Every sheet with a row: time was logged on it, or it was submitted | Code, `timeLogCount` and `totalMinutesLogged` from the sheet's live entries in `dbo.TimesheetProjectTimeLog`. A sheet with no live entry, such as one submitted with no time, keeps its own code |
| **Calculated** (`isCalculated: true`) | Every period with no row, for each live assignment, from the period the user joined the project in up to and including the current one, and not after the project's `EndDate` | The **predicted** code; zero time; `timesheetProjectSubmissionId: null`; status Pending |

#### Query parameters

| Name | Type | Rules |
|---|---|---|
| `userID` | int \| absent | `>= 0`. Absent or `0` = not filtered by user |
| `projectID` | int \| absent | `>= 0`. Absent or `0` = not filtered by project |
| `orgID` | int \| absent | `>= 0`. Absent or `0` = not filtered by organisation |

Any combination works; the filters are ANDed.

| Sent | Returns |
|---|---|
| `userID` only | Every sheet of the user, in every organisation they have projects in |
| `projectID` only | Every user's sheets on the project |
| `orgID` only | Every sheet of every user on every project of the organisation |
| any combination | The sheets matching all of them |
| none | **400** — the whole table is never returned |

#### Example request

```
GET /api/v1/TimesheetProject/get-timesheet-project-sheet?userID=7&projectID=12
```

#### Success response — `200 OK`

`data` is an array of `TimesheetProjectSheetResponse`, ordered by
`periodStartDate`, then project name, then `userId`.

| Field | Type | Notes |
|---|---|---|
| `timesheetProjectSubmissionId` | int \| null | The sheet's id; `null` for a calculated sheet |
| `sheetSubmissionCode` | string | The real code, or the predicted one for a calculated sheet |
| `isCalculated` | bool | `true` for a period with no row yet |
| `isPeriodEnded` | bool | `false` only for the period still running |
| `statusId` | int | 0 Pending, 1 Submitted, 2 Approved, 3 Rejected. A calculated sheet is `0` |
| `statusName` | string | `Pending`, `Submitted`, `Approved` or `Rejected` |
| `userId` | int | |
| `projectId` | int | |
| `projectCode` | string \| null | |
| `projectName` | string \| null | |
| `periodStartDate` | datetime | First day of the period |
| `periodEndDate` | datetime | Last day of the period, inclusive |
| `timeLogCount` | int | Live entries on the sheet |
| `totalMinutesLogged` | int | Their minutes |
| `totalHoursLogged` | decimal | Minutes / 60, rounded to 2 places |
| `submittedBy` | int \| null | |
| `submittedDate` | datetime \| null | UTC |
| `approvedBy` | int \| null | |
| `approvedDate` | datetime \| null | UTC |
| `rejectedBy` | int \| null | |
| `rejectedDate` | datetime \| null | UTC |
| `rejectionReason` | string \| null | |

Every field is always present; a calculated sheet carries the `…By` / `…Date`
fields as `null`.

#### Example response

```json
{
  "success": true,
  "message": "",
  "data": [
    {
      "timesheetProjectSubmissionId": null,
      "sheetSubmissionCode": "PSHT0011",
      "isCalculated": true,
      "isPeriodEnded": true,
      "statusId": 0,
      "statusName": "Pending",
      "userId": 7,
      "projectId": 12,
      "projectCode": "PR0012",
      "projectName": "Website redesign",
      "periodStartDate": "2026-10-06T00:00:00",
      "periodEndDate": "2026-10-12T00:00:00",
      "timeLogCount": 0,
      "totalMinutesLogged": 0,
      "totalHoursLogged": 0,
      "submittedBy": null,
      "submittedDate": null,
      "approvedBy": null,
      "approvedDate": null,
      "rejectedBy": null,
      "rejectedDate": null,
      "rejectionReason": null
    },
    {
      "timesheetProjectSubmissionId": 4,
      "sheetSubmissionCode": "PSHT0004",
      "isCalculated": false,
      "isPeriodEnded": false,
      "statusId": 0,
      "statusName": "Pending",
      "userId": 7,
      "projectId": 12,
      "projectCode": "PR0012",
      "projectName": "Website redesign",
      "periodStartDate": "2026-10-13T00:00:00",
      "periodEndDate": "2026-10-19T00:00:00",
      "timeLogCount": 3,
      "totalMinutesLogged": 450,
      "totalHoursLogged": 7.5,
      "submittedBy": null,
      "submittedDate": null,
      "approvedBy": null,
      "approvedDate": null,
      "rejectedBy": null,
      "rejectedDate": null,
      "rejectionReason": null
    }
  ],
  "errors": []
}
```

#### Empty result

No matching sheets is `data: []`, a 200. So is a project with no `StartDate` or
`SubmissionFrequency`, which has no periods.

#### Error responses

**400** — no filter sent (`userID: userID, projectID or orgID is required.`),
or one is negative.

**500** — unhandled defect, or the tables have not been created in the
database yet.

---

### 53. Submit Project Timesheet

`POST /api/v1/TimesheetProject/submit-timesheet-project-sheet`

**Purpose** — Submit a project timesheet, **with or without time logged**. No
period is sent; it is worked out from the code, or from the project's start
date and frequency:

| `sheetSubmissionCode` sent | What is submitted |
|---|---|
| The code of a stored sheet | That sheet. It must be `userId`'s, on `projectId` |
| A calculated code from [Project Timesheet List](#52-project-timesheet-list) | The period that code is predicted for, worked out exactly as the list works it out. The sheet is created, as Pending with the next code, and submitted with zero time |
| Absent | The **most recent** Pending or Rejected period of `projectId`, stored or calculated, up to the current one |

Then the totals are taken from the time log at that moment, the sheet becomes
**Submitted** with `SubmittedBy` / `SubmittedDate`, and a row is written to
`dbo.TimesheetProjectSubmissionAuditLog`. A Pending sheet can be submitted, and
a Rejected one resubmitted. The period still running can be submitted too, and
nothing more can be logged on it after that.

**Reload the list right before submitting a calculated code.** Predicted codes
move when anyone in the organisation opens a sheet, so a code fetched earlier
can now point to a different period of the same project — see
[Known gaps](#known-gaps) 58. A code that matches no sheet any more is a 400
that says to reload. The response always carries the period and code actually
submitted, and the message names the code.

#### Request body — `TimesheetProjectSheetSubmitRequest` (JSON)

| Field | Type | Required | Rules |
|---|---|---|---|
| `sheetSubmissionCode` | string \| null | no | A code from the list. Absent = the most recent unsubmitted period |
| `userId` | int | yes | `> 0`. Whose sheet it is |
| `projectId` | int | yes | `> 0` |
| `addedBy` | int | yes | `> 0`. Written to `SubmittedBy` and the audit log. Temporary — from the token once authentication is on |

#### Example request

```json
{ "sheetSubmissionCode": "PSHT0011", "userId": 7, "projectId": 12, "addedBy": 7 }
```

#### Success response — `200 OK`

`data` is the submitted sheet, in the same shape as
[Project Timesheet List](#52-project-timesheet-list), with `isCalculated: false`:

```json
{
  "success": true,
  "message": "Timesheet PSHT0011 submitted.",
  "data": {
    "timesheetProjectSubmissionId": 11,
    "sheetSubmissionCode": "PSHT0011",
    "isCalculated": false,
    "isPeriodEnded": true,
    "statusId": 1,
    "statusName": "Submitted",
    "userId": 7,
    "projectId": 12,
    "projectCode": "PR0012",
    "projectName": "Website redesign",
    "periodStartDate": "2026-10-06T00:00:00",
    "periodEndDate": "2026-10-12T00:00:00",
    "timeLogCount": 0,
    "totalMinutesLogged": 0,
    "totalHoursLogged": 0,
    "submittedBy": 7,
    "submittedDate": "2026-10-20T10:04:51.330",
    "approvedBy": null,
    "approvedDate": null,
    "rejectedBy": null,
    "rejectedDate": null,
    "rejectionReason": null
  },
  "errors": []
}
```

#### Error responses

**400** — `userId`, `projectId` or `addedBy` `<= 0` (every failure is
listed), or one of these, one at a time:

| Cause | Message |
|---|---|
| No such project | `ProjectId: Project '12' does not exist.` |
| Project not set up | `Project '12' needs a start date, a submission frequency and an organization before time can be logged or submitted on it.` |
| Code is another user's or project's sheet | `Timesheet 'PSHT0004' is not user '7''s timesheet for project '12', so it cannot be submitted as theirs.` |
| Code matches no sheet (any more) | `SheetSubmissionCode: SheetSubmissionCode 'PSHT0011' is not a timesheet of user '7' on project '12'. Codes of unsaved timesheets move when another timesheet is opened - get the timesheets again and submit the code returned.` |
| User not on the project | `User '7' is not assigned to project '12'.` (also `... (or the assignment was removed), so they have no timesheets on it to submit.` with no code) |
| Project has not started (no code sent) | `Project '12' starts on 2026-11-01; its first timesheet period has not begun yet.` |

**409** — the sheet is already submitted or approved
(`Timesheet 'PSHT0004' is already Submitted.`), or, with no code, every
period of the project is:
`Every timesheet of user '7' on project '12' is already submitted or approved.`

**500** — unhandled defect, or the tables have not been created in the
database yet.

---

### 54. Approve / Reject Project Timesheet

`POST /api/v1/TimesheetProject/update-timesheet-project-sheet-status`

**Purpose** — Approve or reject a **submitted** project timesheet: `statusId`
`2` approves (`ApprovedBy` / `ApprovedDate`), `3` rejects (`RejectedBy` /
`RejectedDate` / `RejectionReason`). The change and its row in
`dbo.TimesheetProjectSubmissionAuditLog` are written in one save. An approved
sheet is final. A rejected one goes back to the user, who can change its time
and resubmit it.

#### Request body — `TimesheetProjectSheetStatusUpdateRequest` (JSON)

| Field | Type | Required | Rules |
|---|---|---|---|
| `timesheetProjectSubmissionId` | int | yes | `> 0`. A stored sheet's id, from the list |
| `statusId` | int | yes | `2` Approved or `3` Rejected |
| `rejectionReason` | string \| null | when rejecting | Required for `3`, at most 500 characters; ignored for `2` |
| `addedBy` | int | yes | `> 0`. Written to `ApprovedBy` / `RejectedBy` and the audit log. Temporary — from the token once authentication is on |

#### Example request

```json
{
  "timesheetProjectSubmissionId": 11,
  "statusId": 3,
  "rejectionReason": "Missing Friday's hours.",
  "addedBy": 3
}
```

#### Success response — `200 OK`

`data` is the sheet after the change, in the same shape as
[Project Timesheet List](#52-project-timesheet-list), with
`"message": "Timesheet PSHT0011 rejected."` (or `approved.`).

#### Error responses

**400** — every failing field is listed together, e.g.
`StatusId: StatusId must be 2 (Approved) or 3 (Rejected).`,
`RejectionReason: RejectionReason is required when rejecting a timesheet.`

**404** — no live sheet has that id: `Timesheet '11' was not found.`

**409** — the sheet is not Submitted:

```json
{ "success": false, "message": "Timesheet 'PSHT0011' is Approved, so it cannot be approved or rejected; only a submitted timesheet can.", "data": null, "errors": [] }
```

**500** — unhandled defect, or the tables have not been created in the
database yet.

---

## Health endpoints

Anonymous, outside the `ApiResponse` envelope, and outside `/api/v1`.

| Endpoint | Purpose |
|---|---|
| `GET /health` | Overall answer — every check |
| `GET /health/live` | Liveness — the process is alive. Says nothing about dependencies. Answer to "restart me?" |
| `GET /health/ready` | Readiness — the API can actually serve traffic; verifies the connection string really reaches SQL Server. Answer to "send me traffic?" |

`200` when healthy, `503` when unhealthy.

#### Example response

```json
{
  "status": "Healthy",
  "totalDurationMs": 42.118,
  "checks": [
    { "name": "self", "status": "Healthy", "description": "The API is running." },
    { "name": "database", "status": "Healthy", "description": null }
  ]
}
```

Check descriptions are included; **exception details never are** — these
endpoints are unauthenticated.

---

## Enumerations

### `TimeLogStatus` — the `Status` column of `dbo.TimeLog`

| Name | Value | Meaning |
|---|---|---|
| `Save` | `1` | A saved entry |
| `Draft` | `2` | A draft entry |

No other value is in use. Sent and received as the **name** (`"Save"`); the
number is also accepted on input. The values are persisted, so they must stay
stable.

This enum covers `dbo.TimeLog` **only** — other tables have their own `Status`
column with its own meaning, and each gets its own type.

### `TimesheetSubmissionStatus` — the `StatusID` column of `dbo.TimesheetSubmission`

| Name | Value | Meaning |
|---|---|---|
| `Submitted` | `1` | Handed in, awaiting review — every new submission |
| `Approved` | `2` | Approved by an admin |
| `Rejected` | `3` | Rejected by an admin |

Sent and received as the **number**. The same values are written to
`dbo.TimeSheetSubmissionAuditLog.StatusID`, one row per change. Declared in code
as `Constants.TimesheetSubmission.Status`; the values are persisted, so they
must stay stable. [Review Timesheet](#13-review-timesheet) accepts only `2` and
`3`.

### `TimesheetProjectSubmissionStatus` — the `StatusID` column of `dbo.TimesheetProjectSubmission`

| Name | Value | Meaning |
|---|---|---|
| `Pending` | `0` | Open for time — every new sheet, logged or not |
| `Submitted` | `1` | Handed in; its time is locked |
| `Approved` | `2` | Approved — final |
| `Rejected` | `3` | Sent back; open for time again and can be resubmitted |

Sent and received as the **number**, with the name alongside as `statusName` on
the sheet list. The same values are written to
`dbo.TimesheetProjectSubmissionAuditLog.StatusID`, one row per submit, approve
and reject. Declared in code as `Constants.TimesheetProjectSubmission.Status`;
the values are persisted, so they must stay stable.
[Approve / Reject Project Timesheet](#54-approve--reject-project-timesheet)
accepts only `2` and `3`.

### `TimesheetProjectTimeLogStatus` — the `Status` column of `dbo.TimesheetProjectTimeLog`

| Name | Value | Meaning |
|---|---|---|
| `Save` | `1` | A saved entry |
| `Draft` | `2` | A draft entry |

Sent and received as the **number** — unlike `TimeLogStatus`, which uses the
name. Declared in code as `Constants.TimesheetProjectTimeLog.Status`. Both
values count towards the sheet's totals.

### `RoleType`

| Name | Value |
|---|---|
| `Employee` | `1` |
| `Manager` | `2` |
| `Administrator` | `3` |

Persisted in the JWT `RoleId` claim. Not reachable through any endpoint today —
authentication is off.

---

## Endpoint summary table

| # | Name | Method | Route | Purpose | Request | Success `data` | Failures |
|---|---|---|---|---|---|---|---|
| 1 | Logged Time List | GET | `/api/v1/TimeLog/get-logged-time-list/{userId}` | Every entry a user has logged, newest first | route param | `TimeLoggedDetailResponse[]` | 404 (route), 500 |
| 2 | Employee Timesheet Setup | GET | `/api/v1/TimeLog/get-timesheet-setup-by-user/{userId}` | A user's timesheet limits, country and time zone (screen view) | route param | `TimesheetMasterSetupResponse`, or `null` when they have none | 404 (route), 500 |
| 3 | Save Time Log (Add / Edit) | POST | `/api/v1/TimeLog/save-employee-time-log` | Log a block of time, or edit one (`timeLogId > 0`) | `TimeLogSaveRequest` | `TimeLogResponse` | 400, 404, 409, 500 |
| 4 | Task List | GET | `/api/v1/TimeLog/get-user-task-list-by-userid/{userId}` | A user's project tasks and sprint tasks, as two lists | route param | `UserTaskListResponse` | 404 (route), 500 |
| 10 | User Dashboard Summary | GET | `/api/v1/TimeLog/get-user-dashboard-summary-by-userID/{userId}` | Logged today / this week, expected, % logged, pending | route param | `UserDashboardSummaryResponse`, or `null` when the user does not exist | 404 (route), 500 |
| 5 | Save Timesheet Setup | POST | `/api/v1/Admin/save-user-timesheet-setup` | Add / update / revive a user's setup; stores `countryId` and `timeZone` as sent | `AdminSaveRequest` | `AdminResponse` | 400, 500 |
| 6 | Admin Timesheet Setup | GET | `/api/v1/Admin/get-user-timesheet-setup/{userID}` | A user's setup (admin view, whole row + `countryName`/`countryWithTimeZone`) | route param | `AdminResponse`, or `null` when they have none | 404 (route), 500 |
| 7 | Delete Timesheet Setup | POST | `/api/v1/Admin/delete-timesheet-setup` | Soft-delete a setup | `AdminDeleteRequest` | `null` | 400, 404, 500 |
| 8 | Employee List | GET | `/api/v1/Admin/get-all-employees-by-orgid/{orgID}` | An organisation's employees, countries + head-count totals | route param | `EmployeeListResponse` | 400, 500 |
| 9 | Country Time Zone List | GET | `/api/v1/Admin/get-country-list-with-timezones` | Every country paired with each of its time zones, one entry per zone | — | `CountryTimeZoneResponse[]` (`countryId`, `countryName`, `timeZone`, `optionValue`) | 500 |
| 11 | Admin Dashboard Summary | GET | `/api/v1/Admin/get-admin-dashboard-summary-by-orgID/{orgID}` | Head counts + this week's logged / expected time for an organisation; pending / approved are static `0`. **Procedure still DRAFT — 500 until applied** | route param | `AdminDashboardSummaryResponse` | 400, 500 |
| 12 | Submit Timesheet | POST | `/api/v1/SheetSubmission/submit-timesheet` | Submit a user's sheet; totals its saved entries, status 1, audit logged | `SheetSubmissionRequest` | `SheetSubmissionResponse` | 400, 409, 500 |
| 13 | Review Timesheet | POST | `/api/v1/SheetSubmission/review-timesheet` | Approve (2) or reject (3) a submitted sheet; repeatable; audit logged | `SheetSubmissionReviewRequest` | `SheetSubmissionResponse` | 400, 404, 500 |
| 14 | Current Week Sheet Details | GET | `/api/v1/SheetSubmission/get-current-week-sheet-details/{userID}/{orgID}` | A user's current week: dates, start / end day, sheet code, hours worked / expected / drift | route params | `CurrentWeekSheetDetailResponse` | 404 (route), 500 |
| 15 | Submitted Sheet List | POST | `/api/v1/SheetSubmission/get-submitted-sheet-list` | An organisation's submitted sheets, every user (`userId = 0`) or one | `SubmittedSheetListRequest` | `SubmittedSheetResponse[]` | 400, 500 |
| 16 | User Role List | GET | `/api/v1/Admin/get-user-role-list-by-orgid/{orgID}` | An organisation's live roles, by name | route param | `UserRoleResponse[]` (`roleId`, `roleName`) | 400, 500 |
| 17 | Add / Edit User Role | POST | `/api/v1/Admin/add-edit-user-role` | Add a role (`roleId` 0 / absent) or rename one; name unique per organisation | `UserRoleSaveRequest` | `UserRoleResponse` | 400, 404, 409, 500 |
| 18 | Delete User Role | POST | `/api/v1/Admin/delete-user-role` | Soft-delete a role of an organisation | `UserRoleDeleteRequest` | `null` | 400, 404, 500 |
| 19 | Client List | GET | `/api/v1/MasterAdmin/get-client-list/{orgID}` | An organisation's live clients with their currency, by name. **Procedure still DRAFT — 500 until applied** | route param | `ClientResponse[]` | 400, 500 |
| 20 | Add / Edit Client Setup | POST **form** | `/api/v1/MasterAdmin/add-or-edit-client-setup` | Add (`CL0001`…) or edit a client; uploads `ClientLogo`; currency from country; billing email unique | `ClientSaveRequest` (`multipart/form-data`) | `ClientResponse` | 400, 404, 409, 415, 502, 500 |
| 21 | Remove Client Setup | POST | `/api/v1/MasterAdmin/remove-client-setup` | Soft-delete a client | `ClientDeleteRequest` | `null` | 400, 404, 500 |
| 22 | Project Category List | GET | `/api/v1/Admin/get-timesheet-project-category-by-orgID/{orgID}` | An organisation's live project categories, by name | route param | `ProjectCategoryResponse[]` (`categoryId`, `categoryName`) | 400, 500 |
| 23 | Add / Edit Project Category | POST | `/api/v1/Admin/add-edit-timesheet-project-category` | Add a category (`categoryId` 0 / absent) or rename one | `ProjectCategorySaveRequest` | `ProjectCategoryResponse` | 400, 404, 500 |
| 24 | Remove Project Category | POST | `/api/v1/Admin/remove-timesheet-project-category` | Soft-delete a project category of an organisation | `ProjectCategoryDeleteRequest` | `null` | 400, 404, 500 |
| 25 | Project List | GET | `/api/v1/TimesheetProject/get-timesheet-project-by-orgID/{orgID}` | An organisation's projects with status and client name, by name | route param | `TimesheetProjectResponse[]` | 400, 500 |
| 26 | Add / Edit Project | POST | `/api/v1/TimesheetProject/add-edit-timesheet-project` | Add a project (`projectId` 0 / absent; code `PR0001`… generated) or update one; `clientId` required, currency taken from the client | `TimesheetProjectSaveRequest` | `TimesheetProjectResponse` | 400, 404, 500 |
| 27 | Project Status List | GET | `/api/v1/TimesheetProject/get-timesheet-project-status-by-orgID/{orgID}` | An organisation's live statuses, by id | route param | `TimesheetProjectStatusResponse[]` (`statusId`, `status`, `colorCode`) | 400, 500 |
| 28 | Remove Project | POST | `/api/v1/TimesheetProject/remove-timesheet-project` | Hard-delete a project **and its budget**, together | `TimesheetProjectDeleteRequest` | `null` | 400, 404, 500 |
| 29 | Project Budget List | GET | `/api/v1/TimesheetProject/get-timesheet-project-budget-by-projectID/{projectID}` | A project's budget, with category name | route param | `TimesheetProjectBudgetResponse[]` | 400, 500 |
| 30 | Add / Edit Project Budget | POST | `/api/v1/TimesheetProject/add-edit-timesheet-project-budget` | Save a project's one budget — the project decides add or update; amount = hours × rate | `TimesheetProjectBudgetSaveRequest` | `TimesheetProjectBudgetResponse` | 400, 404, 500 |
| 31 | Remove Project Budget | POST | `/api/v1/TimesheetProject/remove-timesheet-project-budget` | Hard-delete one budget by its id | `TimesheetProjectBudgetDeleteRequest` | `null` | 400, 404, 500 |
| 32 | Organization List | GET | `/api/v1/MasterAdmin/get-all-organization` | Every organisation — id, name, country — by name | — | `OrganizationResponse[]` | 500 |
| 33 | Add / Edit Project Status | POST | `/api/v1/TimesheetProject/add-edit-timesheet-project-status` | Add a status (`statusId` 0 / absent) or edit one, with its colour | `TimesheetProjectStatusSaveRequest` | `TimesheetProjectStatusResponse` | 400, 404, 500 |
| 34 | Remove Project Status | POST | `/api/v1/TimesheetProject/remove-timesheet-project-status` | Soft-delete a project status | `TimesheetProjectStatusDeleteRequest` | `null` | 400, 404, 500 |
| 35 | Organisation Project Budget List | GET | `/api/v1/TimesheetProject/get-timesheet-project-budget-by-orgID/{orgID}` | Budget lines of every project of an organisation, with project and category name | route param | `TimesheetProjectBudgetByOrgResponse[]` | 400, 500 |
| 36 | Create Timesheet User | POST | `/api/v1/TimesheetProject/create-timesheet-user` | Sign a user up and put them on a project; an existing user of the organisation gets the assignment only | `TimesheetUserCreateRequest` | `TimesheetUserCreateResponse` | 400, 409, 500 |
| 37 | Edit Timesheet User | POST | `/api/v1/TimesheetProject/edit-timesheet-user` | Edit an assignment; name/email saved to `dbo.Signup` when changed | `TimesheetUserUpdateRequest` | `TimesheetUserResponse` | 400, 404, 409, 500 |
| 38 | Remove Timesheet User | POST | `/api/v1/TimesheetProject/remove-timesheet-user` | Take a user off a project by `userId` + `projectId` (soft delete); the user is not touched | `TimesheetUserDeleteRequest` | `null` | 400, 404, 500 |
| 39 | Organisation Timesheet User List | GET | `/api/v1/TimesheetProject/get-timesheet-user-by-orgID/{orgID}` | Every live project assignment of every live user of an organisation, with project and role name | route param | `TimesheetUserListResponse[]` | 400, 500 |
| 40 | Holiday List | GET | `/api/v1/TimesheetProject/get-timesheet-holiday-setup?orgID=&projectID=` | Live holidays of an organisation, a project or both, by date, with country, client and project names | query params (at least one) | `TimesheetHolidaySetupResponse[]` | 400, 500 |
| 41 | Add / Edit Holiday | POST | `/api/v1/TimesheetProject/add-edit-timesheet-holiday-setup` | Add a holiday (`timesheetHolidaySetupId` 0 / absent) or edit one, with its description | `TimesheetHolidaySetupSaveRequest` | `TimesheetHolidaySetupResponse` | 400, 404, 500 |
| 42 | Remove Holiday | POST | `/api/v1/TimesheetProject/remove-timesheet-holiday-setup` | Soft-delete a holiday of an organisation | `TimesheetHolidaySetupDeleteRequest` | `null` | 400, 404, 500 |
| 43 | Weekend Setup List | GET | `/api/v1/TimesheetProject/get-timesheet-weekend-setup?orgID=&projectID=` | Live weekend setups of an organisation, a project or both | query params (at least one) | `TimesheetWeekendSetupResponse[]` | 400, 500 |
| 44 | Add / Edit Weekend Setup | POST | `/api/v1/TimesheetProject/add-edit-timesheet-weekend-setup` | Add a project's weekend setup (`timeSheetWeekendSetupId` 0 / absent) or edit one; one setup per project | `TimesheetWeekendSetupSaveRequest` | `TimesheetWeekendSetupResponse` | 400, 404, 409, 500 |
| 45 | Remove Weekend Setup | POST | `/api/v1/TimesheetProject/remove-timesheet-weekend-setup` | Soft-delete a weekend setup by its id | `TimesheetWeekendSetupDeleteRequest` | `null` | 400, 404, 500 |
| 46 | User Access Setup List | GET | `/api/v1/TimesheetProject/get-user-access-setup?orgID=&userID=` | Module access of live users of an organisation, a user or both | query params (at least one) | `TimesheetRoleAssignmentResponse[]` | 400, 500 |
| 47 | Add / Edit User Access Setup | POST | `/api/v1/TimesheetProject/add-edit-user-access-setup` | Add a user's module access (`assignmentId` 0 / absent) or edit it; one setup per user | `TimesheetRoleAssignmentSaveRequest` | `TimesheetRoleAssignmentResponse` | 400, 404, 409, 500 |
| 48 | Remove User Access Setup | POST | `/api/v1/TimesheetProject/remove-user-access-setup` | Hard-delete a user access setup by its id | `TimesheetRoleAssignmentDeleteRequest` | `null` | 400, 404, 500 |
| 49 | Project Time Log List | GET | `/api/v1/TimesheetProject/get-timesheet-project-time-log?userID=&projectID=&fromDate=&toDate=` | A user's project time entries, each with its sheet's code, status and period | query params (`userID` required) | `TimesheetProjectTimeLogResponse[]` | 400, 500 |
| 50 | Add / Edit Project Time Log | POST | `/api/v1/TimesheetProject/add-edit-timesheet-project-time-log` | Log time (`timesheetProjectTimeLogId` 0 / absent) or edit an entry; 24-hour `startTime`/`endTime`, duration worked out; sheet (`PSHT0001`…) opened for the period on its first entry | `TimesheetProjectTimeLogSaveRequest` | `TimesheetProjectTimeLogResponse` | 400, 404, 500 |
| 51 | Remove Project Time Log | POST | `/api/v1/TimesheetProject/remove-timesheet-project-time-log` | Soft-delete a time entry; its sheet's totals drop | `TimesheetProjectTimeLogDeleteRequest` | `null` | 400, 404, 500 |
| 52 | Project Timesheet List | GET | `/api/v1/TimesheetProject/get-timesheet-project-sheet?userID=&projectID=&orgID=` | Sheets per user, project and period — stored ones with logged time, and calculated ones with a predicted code | query params (at least one) | `TimesheetProjectSheetResponse[]` | 400, 500 |
| 53 | Submit Project Timesheet | POST | `/api/v1/TimesheetProject/submit-timesheet-project-sheet` | Submit a sheet by code, or the most recent unsubmitted one; a period with no time is created and submitted empty; audit logged | `TimesheetProjectSheetSubmitRequest` | `TimesheetProjectSheetResponse` | 400, 409, 500 |
| 54 | Approve / Reject Project Timesheet | POST | `/api/v1/TimesheetProject/update-timesheet-project-sheet-status` | Approve (2) or reject (3, with reason) a submitted sheet; audit logged | `TimesheetProjectSheetStatusUpdateRequest` | `TimesheetProjectSheetResponse` | 400, 404, 409, 500 |
| — | Health | GET | `/health`, `/health/live`, `/health/ready` | Liveness / readiness | — | *(unenveloped)* | 503 |

---

## Known gaps

Documented so nobody has to rediscover them:

1. **No authentication or authorization on any endpoint.** `userId`, `createdBy`,
   `deletedBy` and `adminId` are trusted from the payload. They are marked
   temporary and disappear when JWT is switched back on. In particular **anyone
   can review a timesheet or list an organisation's submissions**: `adminId` is
   not checked against any role, and the admin check in
   `spc_GetSubmittedSheetList` is still a placeholder.
2. **Logged Time List has no paging and no date filter.** Since 2026-09-25
   `spc_GetTimeLoggedDetailsForTask` takes only the user, so a user's whole
   history comes back in one call and the response grows without bound.
3. **No foreign key ties the setup's day columns to `dbo.DayMaster`.** A row can
   hold an id the lookup does not contain; the API treats such a value as "no
   week configured" rather than failing the read.
4. **No delete for a time log entry.** Save Time Log adds and edits; nothing
   removes an entry.
5. **The timesheet setup procedure does not guarantee uniqueness.** When a user
   has more than one row, the first is returned and a warning is logged.
6. **`dbo.Signup` is not recorded at all.** The partial
   `docs/database/schema/dbo.Signup.sql` was removed, although
   `spc_GetEmployeeListByPOrgID`, both dashboard procedures,
   `spc_GetSubmittedSheetList` and `docs/database/README.md` still refer to it. Every procedure that joins it
   compiles in the integration container (deferred name resolution) and fails at
   run time with `Invalid object name 'Signup'`.
7. **The employee list counts deleted time logs.** `spc_GetEmployeeListByPOrgID`
   does not filter `dbo.TimeLog.IsDeleted`, so `totalLoggedHoursCurrentWeek` can
   exceed what every other read in the API reports for the same week.
8. **`canUserLoggedPreDayTime` is computed on the database server's clock.**
   `spc_GetTimesheetMasterSetupByUserID` derives it from `GETDATE()`, so the
   flag returned by **Employee Timesheet Setup** answers "has the cut-off passed *on the server*",
   not "where the employee is" — it will read wrong for anyone outside the
   server's zone. The write path does not use it: it judges `timeEntryLockAt`
   itself in the employee's zone. Now that the procedure also returns
   `TimeZone`, this could be derived correctly in the service instead.
9. **A `null` `timeEntryLockAt` makes `canUserLoggedPreDayTime` false.** The
   procedure's `CASE` compares against `NULL`, which is `UNKNOWN`, and falls to
   `ELSE 0`. The save ignores the flag (a null cut-off means no lock), but the
   **Employee Timesheet Setup** read still reports `false` for such a user. If
   clients read it, the procedure needs `WHEN tms.TimeEntryLockAt IS NULL THEN 1`.
10. **A setup's `timeZone` is never checked against its country.** Since
    2026-09-22 the save stores `countryId` and `timeZone` verbatim, so a row can
    hold a zone its country does not have, or a `countryId` no country has;
    nothing flags either.
11. **An unrecognised `timeZone` silently falls back to UTC** on the write path,
    with a warning in the log. It keeps an employee working when their setup is
    wrong, but nothing surfaces the problem to a caller.
12. **`sheetCode` allocation is read-then-write, and nothing enforces it.**
    Two saves that overlap can both see "no code yet this week" and open two
    codes for one user's week, or two different users' first entries can read the
    same highest code and share it. Codes are shared across rows by design (one
    per user per week), so a unique index on `SheetCode` alone is no longer a
    fix; closing this needs the allocation serialised (e.g. an app lock or a
    sequence table) by the database owner.
13. **Task List reads two tables that are not recorded.** `dbo.TaskMaster` and
    `dbo.SprintTaskManagement` have never been scripted for this repository, so
    the mapping assumes the ids are `int` and `Taskname` is text. A different
    type fails the endpoint at run time.
14. **The user dashboard's "today" is the database server's date.**
    `spc_GetUserDashboardSummaryByUserID` uses `GETDATE()`, so `todayLogged*` —
    and on the first day of the week, the week itself — is off for part of every
    day for anyone outside the server's zone.
15. **Overlap and daily-maximum checks only see entries starting on the same
    date.** An overnight entry from the previous day is not checked against the
    next morning, and an overnight entry's whole duration counts toward the day
    it starts.
16. **Employee List includes soft-deleted setups.** See Employee List → Things
    worth knowing.
17. **Admin Dashboard Summary's procedure is still a DRAFT** — not applied to any
    database, so the endpoint answers 500 until the owner creates it.
18. **A rejected sheet cannot be resubmitted.** Submit Timesheet answers 409 for
    any sheet code that already has a submission, rejected ones included, so a
    user cannot correct their entries and hand the same sheet in again.
19. **`statusText` names only Submitted.** `spc_GetSubmittedSheetList`'s `CASE`
    covers status 1 alone, so approved and rejected sheets come back with
    `statusText: null` until the owner adds `WHEN 2 THEN 'Approved'` and
    `WHEN 3 THEN 'Rejected'`.
20. **Two simultaneous submits of one sheet: the loser gets a 500, not a 409.**
    The primary key on `Timesheetcode` stops the duplicate row, but the service's
    "already submitted" check runs before the insert, so the second of two
    racing requests fails on the key violation instead.
21. **`orgId` is ignored by Submit, Review and Current Week Sheet Details.**
    Neither `dbo.TimeLog` nor `dbo.TimesheetSubmission` has an organisation
    column; only the Submitted Sheet List filters on it (through `dbo.Signup`).
22. **Admin Dashboard Summary's `pendingTimesheets` / `approvedTimesheets` are
    still static `0`**, although `dbo.TimesheetSubmission` now holds the data to
    count them.
23. **A client's `avatar` stops working after 48 hours.** The document service
    returns a pre-signed S3 URL (`X-Amz-Expires=172800`) and it is stored as
    returned, so Client List hands out dead logo links two days after each
    upload. Fixing it needs the service to return a permanent URL, or the API to
    store the object key and request a fresh signed URL on each read.
24. **A client's logo cannot be removed**, only replaced: Add / Edit Client
    Setup has no field to clear it.
25. **A logo uploaded just before a failed save is left on the document
    service.** Every refusal the API can predict happens before the upload, but
    a failure inside `spc_AddEditClientSetup` after it does not undo the upload.
26. **Billing-email uniqueness is checked, not enforced.** Two adds with the
    same email at the same instant can both pass the check; only a filtered
    unique index on `dbo.TimesheetClientSetup.BillingEmail` (live rows) would
    stop that. `alternateEmail` is not checked at all.
27. **Deleting a role does not check who holds it.** How `dbo.Signup.RoleID`
    relates to `dbo.TimesheetUserRole.RoleID` is not recorded, so Delete User
    Role cannot refuse a role that users are still assigned to.
28. **Clients saved before currencies were derived** may hold a `Currencycode`
    that names no `dbo.CountryCurrency` row; they are listed with
    `currencyName` / `currencySymbol` `null` until edited.
29. **Recorded procedures awaiting confirmation.** `spc_AddEditClientSetup` and
    `spc_GetClientListForMasterAdmin` (currency revision) and
    `spc_AddEditUserRoleByAdmin` are recorded as DRAFT in `docs/database/`.
    If the database holds an older version, the matching endpoint answers 500.
    The same goes for `spc_GetTimesheetProjectCategory` and
    `spc_AddEditTimesheetProjectCategory`: until the owner creates them,
    Project Category List and Add / Edit Project Category answer 500.
30. **Project category names are not unique.** Unlike Add / Edit User Role,
    Add / Edit Project Category does not check for another live category of
    the same name, so an organisation can hold two categories a user cannot
    tell apart in a picker.
31. **Removing a project category does not check what uses it.** Nothing
    records which projects reference a category, so Remove Project Category
    cannot refuse one that is still in use.
32. **The TimesheetProject procedures are DRAFT.** `spc_GetTimesheetProject`,
    `spc_AddEditTimesheetProject` (both now also return `CurrencySymbol`),
    `spc_GetTimesheetProjectStatus`, `spc_AddEditTimesheetProjectStatus`,
    `spc_GetTimesheetProjectBudget`, `spc_AddEditTimesheetProjectBudget`,
    `spc_GetTimesheetProjectBudgetByOrgID`,
    `spc_GetTimesheetUserListByOrgID`, `spc_GetTimesheetHolidaySetup`,
    `spc_AddEditTimesheetHolidaySetup`, `spc_GetTimesheetWeekendSetup`,
    `spc_AddEditTimesheetWeekendSetup`, `spc_GetUserAccessSetup` and
    `spc_AddEditUserAccessSetup` are
    recorded in `docs/database/procedures/` and must be created by hand; each
    matching endpoint answers 500 until its procedure exists in its latest
    version. `spc_Signup_timesheet_user` exists in the database but its
    2026-10-18 revision — the `@Maxloghourday` parameter — does not, so
    [Create Timesheet User](#36-create-timesheet-user) answers 500 until the
    owner applies it.
33. **Project removes are hard deletes.** Neither `dbo.TimesheetProject` nor
    `dbo.TimesheetProjectBudget` has a soft-delete or audit column, so a removed
    project or budget cannot be recovered and nothing records who removed it.
34. **One budget per project is kept by the procedure, not the table.** No
    unique constraint on `dbo.TimesheetProjectBudget.ProjectID`, and no foreign
    key to `dbo.TimesheetProject`, was reported, and `ProjectID` is nullable.
    So rows written before the rule (or by anything other than
    `spc_AddEditTimesheetProjectBudget`) can give a project several budgets,
    or a budget no project. Add / Edit Project Budget updates the lowest
    `budgetId`; the lists show them all; Remove Project Budget removes only
    the `budgetId` named; Remove Project removes them all. A unique constraint
    and a foreign key on `ProjectID`, added by the owner, would close this.
35. **A project's `status` is not checked.** Any positive number is saved —
    including another organisation's status, or a removed one. An unknown
    `status` comes back with `statusName: null`. No foreign keys were
    reported. (`currencyCode` is no longer sent; it is copied from the client.)
36. **The budget endpoints have no organisation check.** Project Budget List
    takes a `projectId` alone and Remove Project Budget a `budgetId` alone, so
    — while authentication is off — any caller can read or remove any
    project's budget.
37. **`projectCode` on a budget is a copy.** It is refreshed from the project's
    `code` on every budget save; budget rows saved before the column was added
    show `projectCode: null` until they are saved again.
38. **`spc_GetOrganizationList` is DRAFT.** Organization List answers 500
    until the owner creates it from `docs/database/procedures/`.
39. **Billing-email uniqueness spans organisations.** Client List is filtered
    by `orgID`, but a billing email is still checked against every
    organisation's clients, not within one.
40. **A client's `organizationId` is not checked against `dbo.Organization`.**
    Any positive number is saved; no foreign key was reported.
41. **A project's client is not checked against the project's organisation.**
    Add / Edit Project only checks that `clientId` names a live client, so a
    project can name another organisation's client.
42. **Project status names are not unique, and a removed status is not checked
    for use.** Add / Edit Project Status does not refuse a second live status
    of the same name in an organisation, and Remove Project Status does not
    refuse a status projects still hold.
43. **Remove Project Status has no organisation check.** It takes a
    `statusId` alone, so — while authentication is off — any caller can
    remove any organisation's status.
44. **Budget hours and rate have no upper limit.** Add / Edit Project Budget
    refuses negatives only. A value — or an hours × rate product — beyond
    `decimal(18,2)` overflows inside SQL Server and answers 500, not 400.
45. **A budget is not checked against its project.** Its period may fall
    outside the project's `startDate` / `endDate`, and its hours and amount may
    exceed the project's `totalHours` / `totalBudget`.
46. **Organisation Timesheet User List lists assignments, not users.** A user
    on several projects appears once per project, and a live user with no live
    assignment does not appear at all.
47. **The module flags are not enforced.** User Access Setup stores which
    modules a user may use, but no endpoint reads them: with authentication
    off, every caller can still reach every module. They are data for a client
    to act on until authorization is switched back on.
48. **A holiday date can be added twice.** Add / Edit Holiday does not refuse a
    second live holiday on the same date for the same organisation, project or
    client, and no unique constraint was reported.
49. **Holiday edits and removes record nobody.** `dbo.TimesheetHolidaysetup`
    has no "updated by" or delete stamp columns, so an edit leaves only the
    original `CreateDate` / `CreatedBy`, and a remove sets `IsDelete` alone.
50. **The holiday and weekend lists by `projectID` alone ignore the
    organisation.** Sent without `orgID`, they return the project's rows
    whichever organisation it belongs to — while authentication is off, any
    caller can read them.
51. **One weekend setup per project is kept by the API and the procedure, not
    the table.** No unique constraint on `dbo.TimeSheet_Weekend_setup.ProjectID`
    was reported. Two saves racing past the API's 409 check are stopped by the
    procedure's locked check, but the loser answers **500**, not 409. Anything
    writing the table directly can still add a second setup. A filtered unique
    index on `ProjectID` `WHERE IsDelete = 0`, added by the owner, would close
    this.
52. **A weekend setup's client is not checked against its project's client.**
    Both must belong to the organisation, but the setup may name a client
    other than the project's own.
53. **Remove Weekend Setup and Remove User Access Setup have no organisation
    check.** Each takes an id alone, so — while authentication is off — any
    caller can remove any organisation's row.
54. **One user access setup per user is kept by the API and the procedure, not
    the table.** No unique constraint on `dbo.Timesheet_role_assignment.UserID`
    was reported, and no primary key on `AssignmentID` either. Racing saves
    behave as in 51 (the loser answers 500). A filtered unique index on
    `UserID` `WHERE UserID IS NOT NULL`, added by the owner, would close this.
55. **User access setup removes are hard deletes.** The table has no
    soft-delete column, so a removed setup cannot be recovered and nothing
    records who removed it.
56. **User access setup reads depend on `dbo.Signup`**, which is not recorded
    (see 6). `spc_GetUserAccessSetup` and `spc_AddEditUserAccessSetup` join it
    for the user's name, email and organisation.
57. **`maxLogHoursPerDay` is stored but never enforced.** The assignment's
    `Max_log_hour_day` is saved and returned, and nothing reads it: the daily
    cap [Save Time Log](#3-save-time-log-add--edit) actually applies is the
    **user's** `dbo.TimesheetMasterSetup.MaxTimeInHrs`, which is per user, not
    per project. A user on two projects has one effective cap across both, and
    the two numbers can disagree with nothing to reconcile them. Until the time
    log reads it, treat it as a number for a client to display.
58. **A predicted sheet code is not reserved.** [Project Timesheet
    List](#52-project-timesheet-list) predicts the code each calculated sheet
    would get. When anyone in the organisation opens a sheet — by logging the
    first entry of a period, or by submitting — every prediction after it moves
    on by one. A calculated code fetched earlier can then point to a
    **different period of the same project**, and
    [Submit Project Timesheet](#53-submit-project-timesheet) submits that
    period. Reload the list right before submitting, and check the period in
    the response. Only a code that matches nothing at all is refused.
59. **Nothing in the database keeps sheets and codes unique.** No unique
    constraint was reported on `dbo.TimesheetProjectSubmission.SheetSubmissionCode`
    or on (`UserID`, `ProjectID`, `PeriodStartDate`). Two requests opening the
    same sheet at the same moment can create two sheets for one period, or two
    sheets with one code. Unique indexes on both — the second filtered
    `WHERE IsDelete = 0` — added by the owner, would close this.
60. **The timesheet list builds a whole organisation per call.** Calculated
    codes must agree across every filter, so the list is built for every user
    and project of the organisation and filtered afterwards — even for one
    user. A large organisation with long-running weekly projects makes it slow.
61. **Project time entries are not checked against each other or a cap.** Two
    entries may overlap, `Max_log_hour_day` (see 57) is not applied, and a
    future `logDate` is accepted.
62. **Project timesheet writes trust `userId` and `addedBy`.** With
    authentication off, any caller can log, edit, remove or submit time as any
    user, and approve or reject any organisation's sheet — the status update
    checks no role and no organisation (see 1).
63. **A resubmitted sheet keeps its last rejection.** After a reject and a
    resubmit, `rejectedBy`, `rejectedDate` and `rejectionReason` still show the
    earlier rejection. The audit log has the full history.
64. **The timesheet list has no user name.** It returns `userId` only, so a
    list across users (by `projectID` or `orgID`) needs a second lookup to show
    who each sheet belongs to.
65. **The three project timesheet tables must exist in the database.**
    `dbo.TimesheetProjectSubmission`, `dbo.TimesheetProjectTimeLog` and
    `dbo.TimesheetProjectSubmissionAuditLog` were recorded on 2026-10-20 from
    the owner's scripts. Until they are created, endpoints 49–54 answer 500
    (`Invalid object name`).
