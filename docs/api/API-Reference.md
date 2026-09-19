# ETimeSheet API Reference

Every endpoint the API exposes today, with its purpose, input payload, success
response and failure responses — each with a worked example.

Generated from the code on branch `develop` (2026-09-17). If you change a
controller, DTO or validator, change this file with it.

---

## Contents

- [Conventions](#conventions)
  - [Base URL](#base-url)
  - [Authentication](#authentication)
  - [The response envelope](#the-response-envelope)
  - [JSON rules](#json-rules)
  - [Error contract](#error-contract)
- [TimeLog](#timelog)
  - [POST /api/v1/TimeLog/get-time-logged-details](#1-post-apiv1timelogget-time-logged-details)
  - [GET /api/v1/TimeLog/get-timesheet-setup-by-user/{userId}](#2-get-apiv1timelogget-timesheet-setup-by-useruserid)
  - [POST /api/v1/TimeLog/save-employee-time-log](#3-post-apiv1timelogsave-employee-time-log)
- [Admin](#admin)
  - [POST /api/v1/Admin/save-user-timesheet-setup](#4-post-apiv1adminsave-user-timesheet-setup)
  - [GET /api/v1/Admin/get-user-timesheet-setup/{userID}](#5-get-apiv1adminget-user-timesheet-setupuserid)
  - [POST /api/v1/Admin/delete-timesheet-setup](#6-post-apiv1admindelete-timesheet-setup)
  - [GET /api/v1/Admin/get-all-employees-by-orgid](#7-get-apiv1adminget-all-employees-by-orgid)
- [Health endpoints](#health-endpoints)
- [Enumerations](#enumerations)
- [Endpoint summary table](#endpoint-summary-table)

---

## Conventions

### Base URL

| Environment | URL |
|---|---|
| Local (HTTPS) | `https://localhost:7041` |
| Local (HTTP) | `http://localhost:5041` |

Swagger UI is served at `/swagger` **in Development only**.

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

Until then, a caller can claim to be any user. Do not expose this API publicly.

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
| `DateTime` | ISO-8601, e.g. `"2026-09-14T00:00:00"`. Date-only columns ignore any time part. |
| Content type | `application/json` in and out. |
| Cancellation | Every action honours client disconnect; an aborted request answers **499**. |

### Error contract

| Thrown | HTTP | When |
|---|---|---|
| `ValidationException` / FluentValidation failure | **400** | The payload is malformed — wrong shape, missing field, out-of-range value |
| `BusinessException` | **400** | The payload is well formed but breaks a rule that needed the database to check |
| `UnauthorizedException` | **401** | Not authenticated *(unreachable today — auth is off)* |
| `ForbiddenException` | **403** | Authenticated but not allowed *(unreachable today)* |
| `NotFoundException` | **404** | The named row does not exist, or was soft-deleted |
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
  "message": "2026-09-13 is a Sunday, which is not a working day in this user's timesheet week (MO to FR).",
  "data": null,
  "errors": []
}
```

Note the difference: shape failures fill `errors[]`, rule failures put a single
sentence in `message` and leave `errors[]` empty.

**Not found (404):**

```json
{
  "success": false,
  "message": "Timesheet setup for user '4242' was not found.",
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

### 1. POST `/api/v1/TimeLog/get-time-logged-details`

**Purpose** — Return every entry a user logged against one task within a date
range, together with the totals for that period: what they worked, what was
expected of them, and what is left.

A POST rather than a GET because every argument travels in the body; nothing is
read from the route or query string.

#### Request body — `TimeLoggedDetailsForTaskRequest`

| Field | Type | Required | Rules | Notes |
|---|---|---|---|---|
| `userId` | int | yes | `> 0` | **Temporary** — moves to the token when auth is on |
| `taskId` | int | yes | `> 0` | The task the time was logged against |
| `startDate` | date | yes | not empty | Inclusive lower bound on the entry's `StartDate` |
| `endDate` | date | yes | not empty, `>= startDate` | Inclusive upper bound |

#### Example request

```json
{
  "userId": 101,
  "taskId": 55,
  "startDate": "2026-09-14T00:00:00",
  "endDate": "2026-09-18T00:00:00"
}
```

#### Success response — `200 OK`

`data` is a `TimeLoggedDetailsForTaskResponse`. `summary` is written first,
deliberately, so the totals are readable without scrolling past the details.

**`summary`** — `TimeLoggedSummaryResponse`, all three values in **decimal
hours** (7½ hours is `7.5`, not `"07:30:00"`):

| Field | Type | Meaning |
|---|---|---|
| `totalWorkInHours` | decimal | Sum of the returned entries' durations |
| `totalExpected` | decimal | Daily maximum from the user's setup × every calendar day in the range, inclusive. `0` when the user has no setup |
| `totalRemaining` | decimal | `totalExpected - totalWorkInHours`. **Goes negative** when the user logged more than expected — that is information, not an error, so it is not clamped |

> `totalExpected` counts *every calendar day*, not working days only. Excluding
> non-working days became possible on 2026-09-17, when `startDay`/`endDay` became
> `DayMaster` day ids, but changing the figure would silently restate every total
> already reported — so it is left as a deliberate decision.

**`details[]`** — `TimeLoggedDetailResponse`:

| Field | Type | Meaning |
|---|---|---|
| `sheetId` | int | The entry's key |
| `sheetCode` | string? | The entry's generated reference — `T0001`, `T0002`, … Null on rows created before generation existed, or holding a hand-entered reference |
| `description` | string? | What was worked on |
| `startDate` | date? | Day the work started |
| `startTime` | string? | Clock time it started, `hh:mm:ss` |
| `endDate` | date? | Day the work ended |
| `endTime` | string? | Clock time it ended, `hh:mm:ss` |
| `status` | enum? | `"Save"` (1) or `"Draft"` (2) |
| `statusName` | string | `"Save"` / `"Draft"`; `""` when the column is null |
| `totalWorkingHours` | decimal? | This entry's duration in hours, 2 dp |
| `totalWorkingMinutes` | int? | This entry's duration in whole minutes |

#### Example response

```json
{
  "success": true,
  "message": "",
  "data": {
    "summary": {
      "totalWorkInHours": 22.50,
      "totalExpected": 40.00,
      "totalRemaining": 17.50
    },
    "details": [
      {
        "sheetId": 8812,
        "sheetCode": "TS-00121",
        "description": "Implemented the save-employee-time-log endpoint.",
        "startDate": "2026-09-14T00:00:00",
        "startTime": "09:00:00",
        "endDate": "2026-09-14T00:00:00",
        "endTime": "12:30:00",
        "status": "Save",
        "statusName": "Save",
        "totalWorkingHours": 3.50,
        "totalWorkingMinutes": 210
      },
      {
        "sheetId": 8815,
        "sheetCode": null,
        "description": "Code review and follow-up fixes.",
        "startDate": "2026-09-15T00:00:00",
        "startTime": "10:00:00",
        "endDate": "2026-09-15T00:00:00",
        "endTime": "18:00:00",
        "status": "Draft",
        "statusName": "Draft",
        "totalWorkingHours": 8.00,
        "totalWorkingMinutes": 480
      }
    ]
  },
  "errors": []
}
```

`sheetCode` is `null` on the second entry rather than missing: every field is
written on every row, so a client can index into the response without checking
whether a key exists.

#### Empty result

A user with no matching entries is **not** an error. You get a `200` with an
empty `details` array and zeroed totals:

```json
{
  "success": true,
  "message": "",
  "data": {
    "summary": { "totalWorkInHours": 0, "totalExpected": 0, "totalRemaining": 0 },
    "details": []
  },
  "errors": []
}
```

#### Error responses

| Status | Cause | Example `message` / `errors` |
|---|---|---|
| **400** | Shape validation | `"UserId: 'User Id' must be greater than '0'."` |
| **400** | Range inverted | `"EndDate: The end of the range must not be earlier than its start."` |
| **500** | Unhandled defect | generic message + correlation id |

```json
{
  "success": false,
  "message": "One or more validation errors occurred.",
  "data": null,
  "errors": [
    "TaskId: 'Task Id' must be greater than '0'.",
    "EndDate: The end of the range must not be earlier than its start."
  ]
}
```

---

### 2. GET `/api/v1/TimeLog/get-timesheet-setup-by-user/{userId}`

**Purpose** — Return the timesheet setup that governs one user: their maximum
loggable time, contract type, the bounds of their timesheet week, and whether
they may still back-date an entry.

This is the **timesheet-screen** view — a seven-column projection from
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
| `canUserLoggedPreDayTime` | bool? | Whether the user may log against an earlier day. Held as 0/1 in the database, surfaced as `true`/`false` |

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
    "canUserLoggedPreDayTime": true
  },
  "errors": []
}
```

#### Error responses

| Status | Cause |
|---|---|
| **404** | The user has no timesheet setup row |
| **404** | `userId` below 1 — no route matches |
| **500** | Unhandled defect |

A missing setup is a 404 rather than an empty 200 on purpose: *"this user has no
configured limits"* is a different answer from *"here are their limits"*, and a
caller that read absent as zero would apply a maximum of nothing.

```json
{
  "success": false,
  "message": "Timesheet setup for user '4242' was not found.",
  "data": null,
  "errors": []
}
```

> If the data holds more than one setup row for a user, the first is returned and
> a warning is logged. The procedure does not guarantee uniqueness.

---

### 3. POST `/api/v1/TimeLog/save-employee-time-log`

**Purpose** — Log one block of time for an employee.

**Insert only.** The entry is created and returned with its generated `sheetId`.
Correcting an existing entry is a separate operation and is not built yet — there
is no sheet id in the payload.

Before the row is stored it is checked against the user's timesheet setup (their
working week, whether they may still back-date, their daily maximum) and against
the entries they already have that day, so two blocks cannot cover the same hour.
**A user with no setup cannot log time at all.**

#### Request body — `TimeLogSaveRequest`

| Field | Type | Required | Rules | Notes |
|---|---|---|---|---|
| `userId` | int | yes | `> 0` | The employee the time belongs to; also selects the setup it is validated against. **Temporary** |
| `taskId` | int | yes | `> 0` | Time is always logged against a task |
| `description` | string? | no | unbounded (`nvarchar(max)`) | What was worked on |
| `startDate` | date | yes | not empty | Day the work started. Time part ignored |
| `endDate` | date? | no | `>= startDate` and `<= startDate + 1 day` | Defaults to `startDate`. Exists only for a shift running past midnight |
| `startTime` | string | yes | `hh:mm:ss`, `00:00:00`–`23:59:59` | Clock time it started — e.g. `"09:00:00"` |
| `endTime` | string | yes | `hh:mm:ss`, `00:00:00`–`23:59:59`, and after `startTime` once both dates are counted | Clock time it ended |
| `status` | enum | yes | `1`/`"Save"` or `2`/`"Draft"` | An entry with no status is neither saved nor drafted |
| `createdBy` | int | yes | `> 0` | Who is recording the entry — not necessarily `userId`, since a manager may log on someone's behalf. **Temporary** |

> **`sheetCode` is not in the payload.** The service generates it — see below.
> Sending one is ignored: the property does not exist on the request.

Duration is measured across **both ends including their dates**, so an overnight
entry (22:00 Monday → 06:00 Tuesday) is valid, while 17:00 → 09:00 on a single
day is not.

#### The generated `sheetCode`

Every saved entry gets a reference, and the caller neither sends one nor chooses
one. It comes back on the response.

```
T0001  T0002  T0003  …  T9998  T9999
                                 ↓  the width runs out
T00001 T00002 T00003  …  T99998 T99999
                                 ↓
T000001 …
```

- The first entry ever generated is **`T0001`**. An empty table, or one holding
  only hand-entered references such as `TS-00121`, both start there — a
  reference that is not `T` followed by digits names no position in the sequence
  and is skipped.
- Each new entry takes the **next number after the highest generated code**, read
  from the table at save time.
- When a width runs out, the sequence **starts a new generation one digit wider,
  back at 1**: `T9999` is followed by `T00001`, `T99999` by `T000001`.

**The limit can never be reached.** Each generation holds nine times as many
codes as the one before, and because the widths differ, a code from one
generation can never equal a code from another — `T0001` and `T00001` are
different strings. The width grows on demand up to the fourteen digits
`varchar(15)` can hold, which is 10¹⁴ codes.

Two details worth knowing:

- **Soft-deleted entries still count.** A deleted row has spent its code, so the
  next entry takes the number after it rather than reusing it.
- **A rejected request consumes nothing.** The code is read after every rule has
  passed, so a 400 or a 409 leaves no gap in the sequence.

#### Example request — ordinary entry

```json
{
  "userId": 101,
  "taskId": 55,
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

#### Success response — `200 OK`

`data` is a `TimeLogResponse` — the entry exactly as it was stored, so a client
that has just logged time can display it back without a second request.

| Field | Type | Meaning |
|---|---|---|
| `sheetId` | int | The generated key — the one field the caller could not have known |
| `sheetCode` | string? | **Generated by the service** — `T0001`, `T0002`, … The caller does not send one and cannot choose one |
| `taskId` | int? | As sent |
| `description` | string? | As sent |
| `userId` | int? | As sent |
| `startDate` | date? | As sent, date only |
| `startTime` | string? | As sent, `hh:mm:ss` |
| `endDate` | date? | **Always written**, even if omitted — defaults to `startDate`, because a row with no end date cannot have its duration computed |
| `endTime` | string? | As sent, `hh:mm:ss` |
| `status` | enum? | `"Save"` or `"Draft"` |
| `statusName` | string | The status spelled out, so clients need not carry the numbers |
| `totalWorkingHours` | decimal | Duration in hours, 2 dp. Computed across both ends including dates, so an overnight entry measures correctly instead of coming out negative |
| `createdBy` | int? | As sent |
| `createDate` | datetime? | When the row was stamped |

#### Example response

```json
{
  "success": true,
  "message": "Time logged.",
  "data": {
    "sheetId": 8931,
    "sheetCode": "T0007",
    "taskId": 55,
    "description": "Implemented the save-employee-time-log endpoint.",
    "userId": 101,
    "startDate": "2026-09-16T00:00:00",
    "startTime": "09:00:00",
    "endDate": "2026-09-16T00:00:00",
    "endTime": "12:30:00",
    "status": "Save",
    "statusName": "Save",
    "totalWorkingHours": 3.50,
    "createdBy": 101,
    "createDate": "2026-09-16T11:04:22.117"
  },
  "errors": []
}
```

#### Error responses

**400 — shape validation** (`errors[]` populated):

| Trigger | Message |
|---|---|
| `userId <= 0` | `UserId is required: an entry must belong to an employee.` |
| `taskId <= 0` | `TaskId is required: time is always logged against a task.` |
| `createdBy <= 0` | `CreatedBy is required: the row records who logged the time.` |
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
| No setup for the user | `User '4242' has no timesheet setup, so there are no limits to check this entry against. An administrator has to create one before they can log time.` |
| Future date | `Time cannot be logged against 2026-12-01 because it has not happened yet.` |
| Back-dating not permitted | `User '101' is not allowed to log time against an earlier day, so 2026-09-10 cannot be used.` |
| Back-dating cut-off passed | `The cut-off for logging time against an earlier day is 18:00, and it has passed, so 2026-09-15 is now locked.` |
| Non-working day | `2026-09-13 is a Sunday, which is not a working day in this user's timesheet week (MO to FR).` |
| Over the daily maximum | `Logging 3.50 hours would bring 2026-09-16 to 9.50 hours, above the 8.00 hour daily maximum in this user's timesheet setup (6.00 hours are already logged).` |

```json
{
  "success": false,
  "message": "Logging 3.50 hours would bring 2026-09-16 to 9.50 hours, above the 8.00 hour daily maximum in this user's timesheet setup (6.00 hours are already logged).",
  "data": null,
  "errors": []
}
```

The daily maximum counts **every live entry on that date, drafts included** — a
draft still occupies the time, and excluding drafts would let a user reach any
total by drafting first.

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
which is how a day is normally filled in.

**500** — unhandled defect.

#### Rule evaluation order

Rules are applied in this order, and the first failure answers:

1. Payload shape (FluentValidation) → **400**
2. User has a timesheet setup → **400**
3. Date is open for logging — not in the future; if back-dated, `canUserLoggedPreDayTime` is true and `timeEntryLockAt` has not passed → **400**
4. Date falls inside the timesheet week, or is the exception day → **400**
5. No overlap with existing entries that day → **409**
6. Day stays within the daily maximum → **400**

> A week that is not configured, or configured with codes the application does
> not recognise, imposes **no** constraint — blocking an employee over a
> half-filled setup they cannot fix would be worse. `exceptionDay` is allowed
> **in addition** to the week, and the week may wrap (a Sunday-to-Thursday week
> is normal in some of this data).

---

## Admin

Controller: `src/ETimeSheet.Api/Controllers/AdminController.cs`
Service: `AdminService` · Prefix: `/api/v1/Admin`

Administrative CRUD over `dbo.TimesheetMasterSetup`, plus the organisation's
employee list. **This is the surface that most obviously needs a permission
behind it** — it has none while authentication is off, and endpoint 7 returns a
whole organisation's staff list to anyone who asks.

---

### 4. POST `/api/v1/Admin/save-user-timesheet-setup`

**Purpose** — Save a user's timesheet setup: adding or updating, whichever
applies. One payload, one endpoint, for all three cases.

**There is no setup id in the payload.** A user holds exactly one setup, so
`userId` is what names the row. Send the same payload every time:

| Existing state | What happens |
|---|---|
| Live row for that user | Updated in place; `updatedBy` / `updateDate` stamped |
| Soft-deleted row for that user | Overwritten **and revived** — `isDelete`, `deleteDate`, `deletedBy` cleared; the original `createdBy` / `createDate` are left alone |
| Nothing | Inserted; `createdBy` / `createDate` stamped |

The response carries the saved row with its `setupId`, so the caller never needs
a second call to find out which happened — and cannot create a duplicate row by
sending the wrong id.

> The deleted-row case is why a revived setup keeps its original key: a user whose
> setup was deleted would otherwise look like a new user and get a second row.

#### Request body — `AdminSaveRequest`

Every field except `userId` and `createdBy` is optional, mirroring the table —
every column other than the key is nullable.

| Field | Type | Required | Rules |
|---|---|---|---|
| `userId` | int | yes | `> 0` — identifies the row to save |
| `maxTimeInHrs` | string? | no | `hh:mm:ss`, `00:00:00`–`23:59:59` — e.g. `"08:00:00"` |
| `maxTimInMins` | string? | no | `hh:mm:ss`, `00:00:00`–`23:59:59` |
| `organizationId` | int? | no | `> 0` when supplied |
| `contractType` | int? | no | `> 0` when supplied |
| `startDay` | int? | no | a `dbo.DayMaster.DayID`, `1`–`7` (1 = Monday … 7 = Sunday). Must be supplied together with `endDay` |
| `endDay` | int? | no | a `dbo.DayMaster.DayID`, `1`–`7`. Must be supplied together with `startDay` |
| `exceptionDay` | int? | no | a `dbo.DayMaster.DayID`, `1`–`7`. A day worked *in addition* to the normal week |
| `countryId` | int? | no | `> 0` when supplied |
| `timeEntryLockAt` | string? | no | `hh:mm:ss`, `00:00:00`–`23:59:59`. Time of day after which back-dated entry is locked |
| `createdBy` | int | yes | `> 0`. Lands in `CreatedBy` on insert, `UpdatedBy` on update/revive. **Temporary** |

> `canUserLoggedPreDayTime` is deliberately **absent** from this payload — it is
> not a column on `dbo.TimesheetMasterSetup`, it is derived by
> `spc_GetTimesheetMasterSetupByUserID`. There is nothing here to store.

> The day fields were two- and three-letter codes (`"MO"`, `"SUN"`) until
> 2026-09-17. They are `dbo.DayMaster` ids now, and are validated against the
> exact range `1`–`7` rather than for shape, because the lookup fixes the
> vocabulary at seven rows.

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
  "timeEntryLockAt": "18:00:00",
  "createdBy": 9
}
```

#### Minimal request

```json
{
  "userId": 101,
  "createdBy": 9
}
```

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
| `countryId` | int? | |
| `timeEntryLockAt` | string? | `hh:mm:ss` |
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
    "timeEntryLockAt": "18:00:00",
    "createdBy": 9,
    "createDate": "2026-09-01T08:15:02.443",
    "updatedBy": 9,
    "updateDate": "2026-09-17T10:22:41.980"
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
| `organizationId <= 0` | `OrganizationId must be greater than 0 when it is supplied.` |
| `contractType <= 0` | `ContractType must be greater than 0 when it is supplied.` |
| `countryId <= 0` | `CountryId must be greater than 0 when it is supplied.` |
| `startDay` out of range | `StartDay must be a DayMaster day id between 1 (Monday) and 7 (Sunday).` |
| `endDay` out of range | `EndDay must be a DayMaster day id between 1 (Monday) and 7 (Sunday).` |
| `exceptionDay` out of range | `ExceptionDay must be a DayMaster day id between 1 (Monday) and 7 (Sunday).` |
| Only one end of the week supplied | `StartDay and EndDay must be supplied together.` |

The three time fields are read by `AdminService` rather than the validator, for
the same reason as on the time-log write. An absent one is not an error; one
that was sent and is malformed is, reported **one at a time**:

| Trigger | Message |
|---|---|
| `maxTimeInHrs` malformed or out of range | `MaxTimeInHrs must be a time of day in hh:mm:ss format, between "00:00:00" and "23:59:59" - for example "09:00:00".` |
| `maxTimInMins` malformed or out of range | `MaxTimInMins must be a time of day in hh:mm:ss format, between "00:00:00" and "23:59:59" - for example "09:00:00".` |
| `timeEntryLockAt` malformed or out of range | `TimeEntryLockAt must be a time of day in hh:mm:ss format, between "00:00:00" and "23:59:59" - for example "09:00:00".` |

```json
{
  "success": false,
  "message": "One or more validation errors occurred.",
  "data": null,
  "errors": [
    "UserId: UserId is required: a setup must belong to a user.",
    "StartDay: StartDay and EndDay must be supplied together."
  ]
}
```

**500** — unhandled defect.

There is no 404 on this endpoint: a user with no setup gets one created.

---

### 5. GET `/api/v1/Admin/get-user-timesheet-setup/{userID}`

**Purpose** — Return the timesheet setup belonging to one user, in the
administrative shape (whole row, audit columns included). A user has at most one,
so this is a single object rather than a list.

Compare with [endpoint 2](#2-get-apiv1timelogget-timesheet-setup-by-useruserid),
which returns the seven-column timesheet-screen projection for the same user.

#### Route parameters

| Parameter | Type | Constraint |
|---|---|---|
| `userID` | int | `:int:min(1)` — below 1 matches no route and returns **404** |

> Spelled `userID`, matching the route token character for character. Swagger UI
> substitutes path parameters **case-sensitively**; a parameter named `userId`
> against a `{userID}` token would send the literal text `{userID}`, fail the
> `:int` constraint and surface as a confusing 401 from the fallback policy.

No request body.

#### Example request

```http
GET /api/v1/Admin/get-user-timesheet-setup/101
```

#### Success response — `200 OK`

`data` is an `AdminResponse` — same shape as
[endpoint 4](#4-post-apiv1adminsave-user-timesheet-setup).

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
    "timeEntryLockAt": "18:00:00",
    "createdBy": 9,
    "createDate": "2026-09-01T08:15:02.443"
  },
  "errors": []
}
```

#### Error responses

| Status | Cause |
|---|---|
| **404** | The user has no setup — **including one that was soft-deleted**. A global query filter hides deleted rows |
| **404** | `userID` below 1 — no route matches |
| **500** | Unhandled defect |

```json
{
  "success": false,
  "message": "Timesheet setup for user '4242' was not found.",
  "data": null,
  "errors": []
}
```

---

### 6. POST `/api/v1/Admin/delete-timesheet-setup`

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
> ([endpoint 4](#4-post-apiv1adminsave-user-timesheet-setup)) revives the
> same row and clears the delete stamps.

---

### 7. GET `/api/v1/Admin/get-all-employees-by-orgid`

**Purpose** — Every employee in one organisation, with their contracted time per
week, what they have logged in the **current Monday–Sunday week**, progress
against the contract, and head-count totals for the same rows.

Executes `dbo.spc_GetEmployeeListByPOrgID`. Every figure in a row is computed by
the procedure and passed straight through; the API adds only the `summary`.

#### Request

| Parameter | In | Type | Required | Rules |
|---|---|---|---|---|
| `orgID` | query | int | yes | `> 0`. Omitted, it binds to `0` and is refused |

```
GET /api/v1/Admin/get-all-employees-by-orgid?orgID=700
```

> **"Employee" is the procedure's definition, not the API's** — `dbo.Signup`
> filtered on `RoleID = 2`. That does **not** line up with
> [`RoleType`](#roletype), where `2` is `Manager`. The two vocabularies genuinely
> differ and nothing reconciles them; the endpoint returns whatever the procedure
> considers an employee.

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
| `setupId` | int? | Their setup's key, or `null` when they have none. **This is the "is this employee set up?" flag** — the expected-time fields are also null for a setup that exists but is half-filled |
| `name` | string? | From `dbo.Signup` |
| `email` | string? | From `dbo.Signup` |
| `expectedHoursPerWeek` | int? | Whole contracted hours per week. Null when there is no setup, or it is incomplete |
| `expectedMinsPerWeek` | int? | The **remainder** that goes with it, not a separate quantity: 37.5 h/week is `37` and `30` |
| `expectedHoursPerWeekText` | string? | The same figure formatted by the procedure — `"37h 30m/week"` |
| `totalLoggedHoursCurrentWeek` | int | Whole hours logged this week. `0` rather than null when nothing was logged |
| `totalLoggedMinsCurrentWeek` | int | The remainder that goes with it |
| `totalLoggedHoursCurrentWeekText` | string? | `"12h 45m"` |
| `progressOnThisWeek` | decimal | Percent of the contracted week logged, **already capped at 100 by the procedure** so a client can draw a bar without clamping it again. `0` when there is nothing to measure against |
| `contractTypeId` | int? | `1` = full time, `2` = part time |
| `contractType` | string? | `"Full Time"` / `"Part Time"`. Null for any other id |

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
        "contractType": "Full Time"
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
        "contractType": null
      }
    ]
  },
  "errors": []
}
```

The second employee has no setup, so `setupId`, every expected-time field and
both contract fields come back as `null`. **They are still there.** Both rows
carry the same thirteen keys, which is what lets a grid bind to the response
without a per-row existence check.

#### Error responses

**400 — the organisation id is missing or not positive** (`errors[]` populated):

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

| # | Method | Route | Purpose | Request | Success `data` | Failures |
|---|---|---|---|---|---|---|
| 1 | POST | `/api/v1/TimeLog/get-time-logged-details` | Entries for a user + task in a date range, with totals | `TimeLoggedDetailsForTaskRequest` | `TimeLoggedDetailsForTaskResponse` | 400, 500 |
| 2 | GET | `/api/v1/TimeLog/get-timesheet-setup-by-user/{userId}` | A user's timesheet limits (screen view) | route param | `TimesheetMasterSetupResponse` | 404, 500 |
| 3 | POST | `/api/v1/TimeLog/save-employee-time-log` | Log one block of time (insert only) | `TimeLogSaveRequest` | `TimeLogResponse` | 400, 409, 500 |
| 4 | POST | `/api/v1/Admin/save-user-timesheet-setup` | Add / update / revive a user's setup | `AdminSaveRequest` | `AdminResponse` | 400, 500 |
| 5 | GET | `/api/v1/Admin/get-user-timesheet-setup/{userID}` | A user's setup (admin view, whole row) | route param | `AdminResponse` | 404, 500 |
| 6 | POST | `/api/v1/Admin/delete-timesheet-setup` | Soft-delete a setup | `AdminDeleteRequest` | `null` | 400, 404, 500 |
| 7 | GET | `/api/v1/Admin/get-all-employees-by-orgid` | An organisation's employees + head-count totals | `orgID` query param | `EmployeeListResponse` | 400, 500 |
| — | GET | `/health`, `/health/live`, `/health/ready` | Liveness / readiness | — | *(unenveloped)* | 503 |

---

## Known gaps

Documented so nobody has to rediscover them:

1. **No authentication or authorization on any endpoint.** `userId`, `createdBy`
   and `deletedBy` are trusted from the payload. All three properties are marked
   temporary and disappear when JWT is switched back on.
2. **`totalExpected` counts calendar days, not working days** — now possible to
   change, since the day columns became `DayMaster` ids, but not changed, because
   it would restate figures already reported.
3. **No foreign key ties the setup's day columns to `dbo.DayMaster`.** A row can
   hold an id the lookup does not contain; the API treats such a value as "no
   week configured" rather than failing the read.
4. **No update or delete for a time log entry.** `save-employee-time-log` is
   insert-only.
5. **The timesheet setup procedure does not guarantee uniqueness.** When a user
   has more than one row, the first is returned and a warning is logged.
6. **`dbo.Signup` is only partially recorded.** `docs/database/schema/dbo.Signup.sql`
   was written from the columns `spc_GetEmployeeListByPOrgID` reads, because no
   scripted definition has been supplied. It is enough to stand up the
   integration container; it is not a faithful record of the table.
7. **The employee list counts deleted time logs.** `spc_GetEmployeeListByPOrgID`
   does not filter `dbo.TimeLog.IsDeleted`, so `totalLoggedHoursCurrentWeek` can
   exceed what every other read in the API reports for the same week.
8. **`sheetCode` generation is read-then-write, and nothing enforces
   uniqueness.** Two saves that overlap can read the same highest code and both
   take the next number, producing a duplicate. `dbo.TimeLog` has no unique index
   on `SheetCode` — the primary key is `SheetID` — so the database does not catch
   it either. **Fix: add `CREATE UNIQUE NONCLUSTERED INDEX UX_TimeLog_SheetCode
   ON dbo.TimeLog (SheetCode) WHERE SheetCode IS NOT NULL;`** (filtered, because
   existing rows hold nulls). Once that index exists, a collision becomes a
   failed insert rather than a silent duplicate, and the save can be made to
   retry.
