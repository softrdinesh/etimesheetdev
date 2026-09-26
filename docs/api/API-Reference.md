# ETimeSheet API Reference

Every endpoint the API exposes today, with its purpose, input payload, success
response and failure responses — each with a worked example.

Generated from the code on branch `fix/sheetcode-refactor` (2026-09-27). If you change a
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
6. *Edit only:* the entry as it stands is still open — its current date passes rule 7 and its current start passes rule 9 → **400**
7. Date is open for logging — not in the future; if back-dated and `timeEntryLockAt` is set, it has not passed **in the employee's zone** (a `null` `timeEntryLockAt` never locks) → **400**
8. Date falls inside the timesheet week, or is the exception day → **400**
9. The entry does not start before a `timeEntryLockAt` that has already passed **in the employee's zone** → **400**
10. No overlap with existing entries that day, the edited entry excluded → **409**
11. Day stays within the daily maximum, the edited entry excluded → **400**

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
employee list, its dashboard summary and the country/time-zone lookup. **This is
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
are `exceptionDay` and `timeEntryLockAt`; every other field in the table below
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
  "createdBy": 9
}
```

`"Asia/Kolkata"` is stored because that is what was sent — the country is not
consulted. See [`countryId` and `timeZone` are stored verbatim](#countryid-and-timezone-are-stored-verbatim).

#### Minimal request

Only `exceptionDay` and `timeEntryLockAt` can be left out, so the minimum is
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
error; `timeEntryLockAt` is still optional, and absent is simply `null`.

| Trigger | Message |
|---|---|
| `maxTimeInHrs` missing | `MaxTimeInHrs is required, as a time of day in hh:mm:ss format - for example "09:00:00".` |
| `maxTimeInHrs` malformed, or outside a day | `MaxTimeInHrs must be a time of day in hh:mm:ss format, between "00:00:00" and "23:59:59" - for example "09:00:00".` |
| `maxTimeInHrs` above `23:00:00` | `MaxTimeInHrs must be between "00:00:00" and "23:00:00".` |
| `maxTimInMins` missing | `MaxTimInMins is required, as a time of day in hh:mm:ss format - for example "09:00:00".` |
| `maxTimInMins` malformed, or outside a day | `MaxTimInMins must be a time of day in hh:mm:ss format, between "00:00:00" and "23:59:59" - for example "09:00:00".` |
| `maxTimInMins` above `00:59:00` | `MaxTimInMins must be between "00:00:00" and "00:59:00".` |
| `timeEntryLockAt` sent and malformed | `TimeEntryLockAt must be a time of day in hh:mm:ss format, between "00:00:00" and "23:59:59" - for example "09:00:00".` |

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
> same row and clears the delete stamps.

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
| — | Health | GET | `/health`, `/health/live`, `/health/ready` | Liveness / readiness | — | *(unenveloped)* | 503 |

---

## Known gaps

Documented so nobody has to rediscover them:

1. **No authentication or authorization on any endpoint.** `userId`, `createdBy`
   and `deletedBy` are trusted from the payload. All three properties are marked
   temporary and disappear when JWT is switched back on.
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
   `spc_GetEmployeeListByPOrgID`, both dashboard procedures and
   `docs/database/README.md` still refer to it. Every procedure that joins it
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
