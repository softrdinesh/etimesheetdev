# ETimeSheet API

A production-ready ASP.NET Core Web API on **.NET 6**, using **Entity Framework
Core** against **SQL Server**, with JWT bearer authentication, centralised
authorization, FluentValidation, Swagger, health checks and a full unit +
integration test suite.

The solution currently ships a single feature module — **TimeLog** — with a
single read endpoint, implemented end to end as the reference pattern for every
module and operation added later.

---

## Contents

- [Architecture](#architecture)
- [Project structure](#project-structure)
- [Technologies](#technologies)
- [Prerequisites](#prerequisites)
- [Setup](#setup)
- [SQL Server configuration](#sql-server-configuration)
- [JWT configuration](#jwt-configuration)
- [Database-first: how schema changes](#database-first-how-schema-changes)
- [Running the API](#running-the-api)
- [Swagger](#swagger)
- [Health checks](#health-checks)
- [CORS](#cors)
- [Running the tests](#running-the-tests)
- [How the integration tests work](#how-the-integration-tests-work)
- [The TimeLog API](#the-timelog-api)
- [Adding a new repository](#adding-a-new-repository)
- [Adding a new service](#adding-a-new-service)
- [Adding a new feature](#adding-a-new-feature)

---

## Architecture

Four layers, with a strictly one-way dependency graph.

```
                          HTTP
                            │
                            ▼
                  ┌───────────────────┐
                  │  TimeLogController │   thin: bind, call, return
                  └─────────┬─────────┘
                            │
                            ▼
                  ┌───────────────────┐
                  │   ITimeLogService │   the contract
                  └─────────┬─────────┘
                            │
                            ▼
                  ┌───────────────────┐
                  │   TimeLogService  │   business rules
                  └─────────┬─────────┘
                            │
          ┌─────────────────┴──────────────────┐
          ▼                                    ▼
  ICurrentUserService              IAuthorizationService
                            │
                            ▼
                  ┌───────────────────┐
                  │ ITimeLogRepository│
                  └─────────┬─────────┘
                            ▼
                  ┌───────────────────┐
                  │  TimeLogRepository│   EF Core only
                  └─────────┬─────────┘
                            ▼
                  ┌───────────────────┐
                  │Context│
                  └─────────┬─────────┘
                            ▼
                        SQL Server
```

### The rules that hold it together

1. **Every feature has two separate abstractions** — a service and a repository:
   `ITimeLogService` → `TimeLogService`, `ITimeLogRepository` → `TimeLogRepository`.
   They are never merged and neither inherits the other.
2. **Every public method of a service or repository is declared on its
   interface.** Private helpers are not. The interface is the application
   contract, so nothing is added to it just to help a test.
3. **Controllers are thin.** Routes, binding, a service call, an HTTP result.
   No business logic, no EF Core, no `DbContext`, no repository calls, no role checks.
4. **Services own business logic** and reach data only through repository
   interfaces. They never see `DbContext`.
5. **Repositories own data access** and never make authorization decisions.

Points 1–4 are not just conventions here — `ETimeSheet.Tests/Unit/Architecture`
asserts them, so a violation fails `dotnet test`. In particular, `Application`
holds no reference to `Infrastructure` or to EF Core, which makes "a service
cannot touch `DbContext`" a compile error rather than a review comment.

---

## Project structure

```
ETimeSheet.sln
global.json                      SDK pinned to .NET 6
Directory.Build.props            TFM, nullable, implicit usings for all projects
docker-compose.yml               local DEVELOPMENT SQL Server (not used by tests)
docs/database/                   the recorded DB schema + changelog (source of truth)
scripts/                         run-unit-tests, run-integration-tests,
                                 run-all-tests, ensure-docker

src/
├── ETimeSheet.Api/                      → Application, Infrastructure, Shared
│   ├── Controllers/TimeLogController.cs
│   ├── Middleware/ExceptionHandlingMiddleware.cs
│   ├── Extensions/                      authentication, CORS, Swagger, health,
│   │                                    API services, middleware, DB startup
│   ├── Configuration/                   options binding + validation
│   ├── Program.cs
│   └── appsettings.json                 single settings file, all environments
│
├── ETimeSheet.Application/               → Shared   (no EF Core, no Infrastructure)
│   ├── Models/
│   │   ├── Entities/TimeLog.cs
│   │   ├── Common/                       AuditableEntity, AccessToken
│   │   └── Results/                      TimeLoggedDetail (keyless SP result)
│   ├── DTOs/TimeLogs/                    TimeLoggedDetailsForTaskRequest,
│   │                                     TimeLoggedDetailResponse
│   ├── Validators/TimeLogs/              TimeLoggedDetailsForTaskRequestValidator
│   ├── Interfaces/
│   │   ├── Repositories/ITimeLogRepository.cs
│   │   └── Services/                     ICurrentUserService, ICacheService,
│   │                                     IDateTimeProvider, ITokenService
│   ├── Services/
│   │   ├── Interfaces/                   ITimeLogService, IAuthorizationService
│   │   └── Implementations/              TimeLogService, AuthorizationService
│   └── Common/                           mapping, DI composition
│
├── ETimeSheet.Infrastructure/            → Application, Shared
│   ├── Data/
│   │   ├── Context.cs
│   │   ├── Configurations/               TimeLogConfiguration,
│   │   │                                 TimeLoggedDetailConfiguration
│   │   └── Interceptors/AuditableEntityInterceptor.cs
│   ├── Repositories/TimeLogRepository.cs
│   ├── Services/                         CurrentUserService, MemoryCacheService,
│   │                                     TokenService, SystemDateTimeProvider
│   └── DependencyInjection/
│
└── ETimeSheet.Shared/                    → no project dependencies
    ├── Constants/                        ClaimConstants, Permissions
    ├── Enums/                            RoleType, TimeLogStatus
    ├── Exceptions/                       AppException + the six typed failures
    ├── Responses/                        ApiResponse<T>
    ├── Configuration/                    Jwt/Cache/Database/Cors settings
    └── Utilities/                        DateTimeExtensions

tests/
└── ETimeSheet.Tests/                     → all projects
    ├── Unit/                             Services, Validators, Utilities, Architecture
    │                                     (no Docker, no database)
    ├── Integration/                      endpoints, authentication, health
    │                                     (real SQL Server in Docker)
    ├── Fixtures/
    │   ├── SqlServerFixture.cs           owns the throwaway container
    │   ├── ETimeSheetApiFactory.cs       boots the API against it
    │   ├── IntegrationTestCollection.cs  one container for the whole run
    │   └── IntegrationTestBase.cs        clean database per test
    ├── Helpers/                          harness, frozen clock, token factory
    └── TestData/TimeLogTestData.cs
```

> **Note on one folder placement.** Repository *interfaces* live in
> `Application/Interfaces/Repositories/` rather than under `Infrastructure`.
> Putting them in Infrastructure would force `Application` to reference
> `Infrastructure`, which drags EF Core into the business layer and loses the
> compile-time guarantee described above. Responsibilities are unchanged: every
> repository still has its own dedicated interface.

---

## Technologies

| Concern | Choice |
|---|---|
| Runtime | .NET 6 (`net6.0`, pinned in `global.json` and `Directory.Build.props`) |
| API | ASP.NET Core Web API, controllers |
| Data access | Entity Framework Core 6 + SQL Server (**no Dapper, no DbUp**) |
| Auth | JWT bearer (`Microsoft.AspNetCore.Authentication.JwtBearer`) |
| Validation | FluentValidation 11 with auto-validation |
| Caching | `IMemoryCache` behind `ICacheService` (registered and tested; nothing is cached yet) |
| API docs | Swashbuckle / OpenAPI with a bearer scheme |
| Health | `Microsoft.Extensions.Diagnostics.HealthChecks` + `AddDbContextCheck` |
| Tests | xUnit, Moq, `WebApplicationFactory`, Testcontainers (real SQL Server in Docker) |

Nullable reference types and implicit usings are enabled everywhere, and
nullability warnings are promoted to errors. Everything is `async`/`await` with
`CancellationToken` support end to end.

---

## Prerequisites

| Tool | Needed for | Notes |
|---|---|---|
| **.NET 6 SDK** | everything | `global.json` pins it, so newer SDKs on the machine are ignored |
| **Docker** | the development database and the integration tests | not needed for building or for unit tests |

Docker has to be installed once, by hand — it needs administrator rights and a
licence acceptance that a script should not make on your behalf. Everything
after that is automated: the scripts start Docker, pull the SQL Server image and
manage the containers.

```bash
# macOS
brew install --cask docker
# or a lighter alternative
brew install colima docker && colima start --arch x86_64 --memory 4
```

> **Apple Silicon.** The SQL Server image is published for **amd64 only**. Open
> Docker Desktop → **Settings → General** and enable
> **“Use Rosetta for x86/amd64 emulation”**. Without it the container cannot
> start. Expect SQL Server to take 20–40 seconds to come up under emulation.

## Setup

```bash
dotnet restore
dotnet build
./scripts/run-unit-tests.sh          # fast, no Docker needed
docker compose up -d                 # a local SQL Server, if you want one
dotnet run --project src/ETimeSheet.Api
```

Verify the SDK:

```bash
dotnet --version     # expect 6.0.xxx
```

---

## SQL Server configuration

The connection string is bound to `DatabaseSettings` from the `Database` section.
`appsettings.json` ships it **empty on purpose** — a real connection string must
never be committed.

For local development, start the containerised SQL Server:

```bash
docker compose up -d
```

That brings up `docker-compose.yml` (SQL Server 2022 on `localhost:1433`, data in
a named volume). The credentials match `appsettings.json` out of the box.

**It starts empty.** This project is database-first and creates no schema, so you
have to apply the objects recorded in [`docs/database/`](docs/database/) yourself
— or point the connection string at a database that already has them.

```bash
docker compose up -d      # start it
docker compose down       # stop it, keep the data
docker compose down -v    # stop it and delete the data
docker compose logs -f sqlserver
```

Override the defaults with `MSSQL_SA_PASSWORD` and `MSSQL_PORT` if 1433 is taken.

If you would rather point at your own SQL Server instance, edit
`appsettings.json` or — preferably — override it without touching the file. Any
of these outranks the file, in increasing order of precedence: an environment
variable `Database__ConnectionString`, or user secrets:

```bash
cd src/ETimeSheet.Api
dotnet user-secrets init
dotnet user-secrets set "Database:ConnectionString" \
  "Server=localhost,1433;Database=ETimeSheet;User Id=sa;Password=<your-password>;TrustServerCertificate=True;MultipleActiveResultSets=True"
```

In deployed environments supply it as an environment variable:

```bash
export Database__ConnectionString="Server=...;Database=ETimeSheet;..."
```

Available settings:

| Key | Default | Purpose |
|---|---|---|
| `Database:ConnectionString` | *(empty — required)* | SQL Server connection string |
| `Database:MaxRetryCount` | `3` | Retries for transient SQL failures |
| `Database:CommandTimeoutSeconds` | `30` | Per-command timeout |
| `Database:EnableSensitiveDataLogging` | `false` | Local debugging only — logs parameter values |

Startup fails immediately if the connection string is missing, rather than on
the first request that needs it.

---

## JWT configuration

```json
"Jwt": {
  "SecretKey": "",
  "Issuer": "ETimeSheet",
  "Audience": "ETimeSheetClient",
  "ExpiryMinutes": 60,
  "ClockSkewSeconds": 0
}
```

```bash
cd src/ETimeSheet.Api
dotnet user-secrets set "Jwt:SecretKey" "$(openssl rand -base64 48)"
```

- The secret must be at least 32 characters (HMAC-SHA256). Startup fails
  otherwise.
- Issuer, audience, signing key, lifetime and expiry are **all** validated.
- `ClockSkewSeconds` defaults to `0` rather than the framework's five minutes,
  so expiry means expiry.
- Tokens are never logged; authentication failures log only the reason.

### Claims

Every token carries the user and role identity under centralised claim names
(`ClaimConstants.UserId` = `uid`, `ClaimConstants.RoleId` = `rid`):

```json
{ "jti": "…", "uid": "1001", "rid": "2", "iss": "ETimeSheet", "aud": "ETimeSheetClient", "exp": … }
```

Application code reads them **only** through `ICurrentUserService`:

```csharp
int userId = _currentUserService.GetRequiredUserId();   // throws 401 if absent
int roleId = _currentUserService.GetRequiredRoleId();
```

A missing, unparsable or non-positive claim is reported as "no identity" and
produces a 401 — never a silent `0`.

### Issuing a token

`ITokenService` / `TokenService` mints tokens and is registered in DI, ready for
the login endpoint that arrives with the User module. There is deliberately no
token-issuing endpoint yet, because there is no user store to authenticate
against. The test suite mints tokens with the same service
(`tests/ETimeSheet.Tests/Helpers/TestTokenFactory.cs`), which is the quickest
way to get a token for manual Swagger testing today.

Role ids come from `RoleType`: `1` Employee, `2` Manager, `3` Administrator.

---

## Database-first: how schema changes

**This application never creates, alters or drops schema.** The database is
maintained by hand, outside this repository, and the API only reads and writes
rows.

That is enforced by removal rather than by convention — none of the following
exists in the solution:

| Removed | Why |
|---|---|
| EF Core migrations | Would let the app author schema |
| `Microsoft.EntityFrameworkCore.Design` | The package `dotnet ef` needs |
| `.config/dotnet-tools.json` (`dotnet-ef`) | The migrations CLI |
| `scripts/start-dev-db.sh` | Ran `dotnet ef database update` |
| `ApplicationDbSeeder` + startup seeding | The app writing rows on boot |

There is no `EnsureCreated`, no `Migrate()`, and no `Database:ApplyMigrationsOnStartup`.

### The workflow

1. **You** change the database in SQL Server — add the table, alter the column,
   create the procedure.
2. **You** update the matching file in [`docs/database/`](docs/database/) and add
   a dated entry to its `CHANGELOG.md`.
3. **Then** the entity, its `IEntityTypeConfiguration<T>`, and any repository
   method are updated to match.

Step 3 is not optional. EF Core mappings are written against what is recorded in
`docs/database/`; if the real database drifts from it, queries fail at runtime
with `Invalid column name` and nothing catches it at compile time.

### The recorded schema

```
docs/database/
├── README.md         the rule, and how to regenerate from a live database
├── CHANGELOG.md      dated record of every change
├── schema/           one file per table
└── procedures/       one file per stored procedure / function
```

These files are documentation. **Nothing in the API executes them.** The only
thing that does is the integration test fixture, which runs them against a
disposable Testcontainers instance that is destroyed at the end of the run — so
the tests and the history can never disagree, because there is one copy.

---

## Running the API

```bash
docker compose up -d                          # a database to point at
dotnet run --project src/ETimeSheet.Api
```

Defaults to `https://localhost:7041` and `http://localhost:5041`, and opens
Swagger UI. Startup creates no schema and writes no rows — the database must
already contain the objects in [`docs/database/`](docs/database/).

---

## Swagger

Swagger UI is served at **`/swagger`**, and the OpenAPI document at
**`/swagger/v1/swagger.json`**.

```bash
dotnet run --project src/ETimeSheet.Api      # opens /swagger automatically
```

The document declares a single HTTP bearer security scheme and applies it at the
document level, so the **Authorize** button drives every endpoint:

```jsonc
"components": { "securitySchemes": { "Bearer": { "type": "http", "scheme": "bearer", "bearerFormat": "JWT" } } },
"security": [ { "Bearer": [] } ]
```

To call the endpoint from the UI:

1. Click **Authorize**.
2. Paste the **raw JWT** — Swagger adds the `Bearer ` prefix itself, so pasting
   `Bearer eyJ...` will fail.
3. Close the dialog. The endpoint is now callable with that identity.

There is no login endpoint yet (there is no user store to authenticate against),
so mint a development token with `TokenService` — the same type the API will use
once a User module lands. See [JWT configuration](#jwt-configuration).

What the document exposes today:

| | |
|---|---|
| Endpoints | `GET /api/v1/TimeLog/get-time-logged-details/{userId}/{taskId}` |
| Route parameters | `userId`, `taskId` |
| Documented responses | 200, 400 |
| Schemas | `TimeLoggedDetailResponse`, `TimeLogStatus`, and the `ApiResponse` envelope |

Enum values appear as names (`Approved`) rather than numbers, matching the JSON
the API actually returns.

> **Development only.** `UseSwaggerUi()` is called inside the
> `IsDevelopment()` branch of `Program.cs`. An OpenAPI document is a map of the
> attack surface, so publishing it further should be a deliberate decision — move
> that one line out of the branch (ideally behind a configuration flag and an
> authorization policy) if you want it in a deployed environment.

---

## Health checks

| Endpoint | Reports |
|---|---|
| `GET /health` | Every check |
| `GET /health/live` | Process liveness only (`self`) |
| `GET /health/ready` | Readiness, including SQL Server connectivity (`database`) |

All three are anonymous so a load balancer or orchestrator can reach them, and
they return check names and statuses without exception detail.

```json
{
  "status": "Healthy",
  "totalDurationMs": 12.4,
  "checks": [
    { "name": "self", "status": "Healthy", "description": "The API is running." },
    { "name": "database", "status": "Healthy", "description": null }
  ]
}
```

---

## CORS

```json
"Cors": {
  "AllowedOrigins": [ "http://localhost:5173" ],
  "AllowCredentials": true
}
```

Origins come from configuration only. There is **no** `AllowAnyOrigin()` code
path: an empty allow-list produces a policy that permits nothing, which fails
visibly in the browser instead of quietly opening the API to every site.

---

## Running the tests

The suite is split in two, because only half of it needs Docker.

```bash
./scripts/run-unit-tests.sh          # unit + architecture — no Docker, ~1s
./scripts/run-integration-tests.sh   # real SQL Server in Docker
./scripts/run-all-tests.sh           # both, unit first
```

The raw `dotnet test` equivalents, if you prefer them or are wiring up CI:

```bash
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
dotnet test                                   # everything; needs Docker running
```

`./scripts/run-integration-tests.sh` is the one to reach for. It:

1. checks whether Docker is installed, and prints exact install instructions if not;
2. **starts Docker for you** if it is installed but not running, and waits for the daemon;
3. warns about Rosetta on arm64 and pulls the amd64 image variant explicitly;
4. pulls the pinned SQL Server image if it is missing, so the first run does not
   look like it has hung on a 1.5 GB download;
5. runs `dotnet test --filter Category=Integration`.

Extra arguments are passed straight through:

```bash
./scripts/run-integration-tests.sh --filter "FullyQualifiedName~TimeLogEndpointsTests"
```

Coverage:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

### What each layer of the suite covers

**Unit tests** (61) cover `TimeLogService` — who may see which entries, how a
request becomes a query, and how results are mapped — plus the
`AuthorizationService` role/permission matrix, `CurrentUserService` claim
handling, `MemoryCacheService`, the validator and the date utility. Collaborators
are mocked with Moq; the class under test never is. These need no database and no
Docker.

**Architecture tests** (`Unit/Architecture/ArchitectureRuleTests.cs`) assert the
rules this README describes: every public service and repository method is on its
interface, `Application` references neither EF Core nor `Infrastructure`, `Shared`
references nothing in the solution, only repositories inject
`Context`, and controllers inject service interfaces only. Add new
services and repositories to the `[InlineData]` list when you create them.

**Integration tests** (27) run the real pipeline against a real SQL Server — see
below.

---

## How the integration tests work

```
./scripts/run-integration-tests.sh
        │
        ├── ensure Docker is installed and running (starts it if needed)
        ├── pull mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04
        │
        ▼
  dotnet test --filter Category=Integration
        │
        ▼
  SqlServerFixture  ── Testcontainers ──▶  SQL Server container
        │                                   random free port
        │                                   generated password
        │                                   database: ETimeSheetIntegrationTests
        ├── runs docs/database/**/*.sql            ← the recorded real schema
        │
        ▼
  ETimeSheetApiFactory (WebApplicationFactory<Program>)
        │   real middleware, JWT auth, FluentValidation, controllers,
        │   services, repositories, EF Core, audit interceptor
        │   only the clock is substituted
        ▼
  each test ──▶ ResetDatabaseAsync()  (empties every mapped table)
        │
        ▼
  run ends ──▶ container destroyed
```

### No real or live database, ever

- The container is created **per test run**, on a **random free port**, with a
  **generated password**, and is destroyed when the run ends — including after a
  crash, because `WithCleanUp(true)` hands cleanup to the Testcontainers reaper.
- It has nothing to do with `docker-compose.yml`. That file is your *development*
  database; the tests never read its connection string, its port or its data.
- The connection string is injected into the test host through configuration,
  and the fixture **asserts** before creating the schema that the host actually
  resolved the container's connection string. The API project's
  `appsettings.json` is on the configuration chain and names a local
  SQL Server; the injected value outranks it, but that is an ordering detail, so
  the assertion turns a silent risk — a suite deleting rows from your real
  database — into an immediate, loud failure.

### Why a real SQL Server instead of the in-memory provider

The in-memory provider does not speak SQL. It silently accepts queries SQL Server
would reject, ignores column types, unique indexes and constraints, and cannot
run real DDL. Running the real engine means the suite also proves:

- the **recorded schema** in `docs/database/` actually produces a working
  database — the fixture executes those files rather than calling
  `EnsureCreated`, so a drift between the record and the mappings shows up here;
- EF Core's **SQL translation** works, including `EF.Functions.Like` behind the
  search filter and the global query filter that hides soft-deleted rows;
- **`date` and `datetime2(0)`** columns round-trip the way the business rules assume.

### Test isolation

The container is shared across all integration test classes through one xUnit
collection (`IntegrationTestCollection`), because starting SQL Server is by far
the most expensive thing in the run. That makes those classes run sequentially,
and each test gets a clean slate instead:

```csharp
public Task InitializeAsync() => Factory.ResetDatabaseAsync();
```

`ResetDatabaseAsync` builds its clean-up script **from the EF model**, so a new
entity is covered the moment it is mapped — nobody has to remember to update a
list of tables. It disables constraints, deletes every row, reseeds identity, and
re-enables constraints.

### Writing a new integration test

```csharp
[Trait("Category", "Integration")]
[Collection(IntegrationTestCollection.Name)]
public class CompanyEndpointsTests : IntegrationTestBase
{
    public CompanyEndpointsTests(SqlServerFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Get_ReturnsTheCompany()
    {
        var client = EmployeeClient();          // or ManagerClient()
        // Factory.SeedAsync(...) / Factory.FindAsync(...) to arrange and assert
    }
}
```

The `[Trait]` keeps it out of `run-unit-tests.sh`; the `[Collection]` shares the
one container; the base class gives you a clean database and authenticated clients.

### Configuration knobs

| Variable | Default | Purpose |
|---|---|---|
| `ETIMESHEET_TEST_SQL_IMAGE` | `mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04` | Swap the image — e.g. a natively built arm64 one |
| `DOCKER_WAIT_SECONDS` | `90` | How long the script waits for the Docker daemon |

The image tag is **pinned**, in the fixture and in the script. A floating tag
would let an upstream image change turn a green suite red without a line of code
changing.

### Troubleshooting

| Symptom | Cause and fix |
|---|---|
| `Docker is not installed` | Install it — the script prints the command for your OS |
| `Could not start the SQL Server test container` | Docker is installed but not running. Use the script, which starts it |
| `no matching manifest for linux/arm64` | Apple Silicon without Rosetta. Docker Desktop → Settings → General → enable Rosetta emulation |
| Container start times out | Emulated SQL Server is slow. Give Docker ≥ 4 GB RAM (Settings → Resources) |
| First run seems to hang | It is pulling a ~1.5 GB image. The script pulls it up front so you can see progress |
| Port 1433 already in use | Only affects `docker-compose.yml`; set `MSSQL_PORT=1434`. Tests use a random port and are unaffected |

### CI

```yaml
- uses: actions/setup-dotnet@v4
  with:
    dotnet-version: '6.0.x'
- run: dotnet test --filter "Category=Unit"
- run: dotnet test --filter "Category=Integration"   # needs a Docker daemon
```

GitHub Actions' `ubuntu-latest` runners are amd64 with Docker already running, so
no emulation and no bootstrap step is needed there.

---

## The TimeLog API

One endpoint. Authentication is switched off for now, so it takes no token, and
it returns the standard envelope:

```json
{ "success": true, "message": "", "data": [ ], "errors": [] }
```

| Method | Route |
|---|---|
| `GET` | `/api/v1/TimeLog/get-time-logged-details/{userId}/{taskId}` |

The route lives on the action
(`[HttpGet("get-time-logged-details/{userId:int}/{taskId:int}")]`) under the
controller's `[Route("api/v1/[controller]")]` prefix.

### Route parameters

| Parameter | Type | Meaning |
|---|---|---|
| `userId` | int | Whose entries to return. Must be greater than 0 |
| `taskId` | int | Which task to return them for. Must be greater than 0 |

Both are validated for shape by `TimeLoggedDetailsForTaskRequestValidator`; a
zero or negative value is a 400 in the standard envelope.

```bash
curl "https://localhost:7041/api/v1/TimeLog/get-time-logged-details/1001/42"
```

```json
{
  "success": true,
  "message": "",
  "data": [
    {
      "sheetId": 3,
      "sheetCode": "TS-0001",
      "description": "Sprint planning and backlog refinement.",
      "startDate": "2026-03-09",
      "startTime": "09:00:00",
      "endDate": "2026-03-09",
      "endTime": "12:00:00",
      "status": "Approved",
      "statusName": "Approved"
    }
  ],
  "errors": []
}
```

Not paged: the stored procedure returns the whole result set for one user and
one task, and that set is bounded by the task.

### How it reaches the database

The read is served by an existing stored procedure, not by LINQ:

```sql
EXEC dbo.spc_GetTimeLoggedDetailsForTask @PUserID = @p0, @PTaskID = @p1
```

`TimeLogRepository` calls it with `FromSqlInterpolated`, so the interpolated
values become real `SqlParameter`s and can never be parsed as SQL. The rows
materialise into `TimeLoggedDetail`, a **keyless** type mapped with
`HasNoKey()` + `ToView(null)`: it is a query result, not a table, so it is never
tracked, never written, and implies no schema.

The procedure filters deleted rows itself (`IsDeleted <> 1`). Elsewhere the same
rule is a global query filter on the `TimeLog` entity, written as `IsDeleted != 1`
so the two agree exactly.

> **`userId` comes from the route, not from a token.** That is deliberate and
> temporary. When authentication is turned back on, it must come from the
> authenticated principal via `ICurrentUserService` instead — a caller must not
> be able to read another user's entries by editing the URL.

### Authorization model

> **Nothing enforces this today.** Authentication is switched off, the controller
> is `[AllowAnonymous]`, and `TimeLogService` makes no authorisation call. The
> whole stack below — `IAuthorizationService`, the matrix, `Permissions` — is
> left registered, tested and intact so that turning it back on is a small,
> local change rather than a rewrite.

When it is on, business code asks for a **permission**, never a role id:

```csharp
await _authorizationService.RequireSelfOrPermissionAsync(ownerId, Permissions.TimeLogs.ViewAll, ct);
```

The role → permission matrix lives in exactly one place —
`AuthorizationService.RolePermissions`:

| Permission | Employee | Manager | Administrator |
|---|:--:|:--:|:--:|
| `timelogs.view.all` | | ✓ | ✓ |

Only the permission the API actually enforces is defined. Add a permission when
the operation it guards is built, not before — an unused permission reads as a
granted capability to anyone auditing the matrix.

The final matrix is still to be agreed. When it is, replace
`AuthorizationService.GetPermissions()` with a database lookup — no business rule
mentions a role id, so nothing else has to change.

`IAuthorizationService` keeps its full surface (`RequireRoleAsync`,
`RequireAnyRoleAsync`, `RequirePermissionAsync`, `RequireSelfOrPermissionAsync`,
`HasPermissionAsync`, `IsInRole`) ready for the operations that come next.

### Where the data comes from

There is no write endpoint, and the application seeds nothing. Rows arrive only
from whatever you insert into the database yourself — which is the point of a
database-first project: the data and the schema both belong to the database.

### Error responses

| Status | Exception | Meaning |
|---|---|---|
| 400 | `ValidationException`, `BusinessException` | Malformed query string |
| 401 | `UnauthorizedException` | No token, invalid token, or missing identity claim |
| 403 | `ForbiddenException` | Authenticated but not allowed to see that user's entries |
| 500 | anything else | Logged in full; the response carries only a correlation id |

`NotFoundException` and `ConflictException` are part of the error contract and
are mapped by the middleware, but nothing throws them yet.

---

## Adding a new repository

1. Declare the contract in `Application/Interfaces/Repositories/ICompanyRepository.cs`.
   Every public operation goes here; private query helpers do not.

   ```csharp
   public interface ICompanyRepository
   {
       Task<Company?> GetByIdAsync(int companyId, CancellationToken cancellationToken = default);
       Task<Company?> GetForUpdateAsync(int companyId, CancellationToken cancellationToken = default);
       Task AddAsync(Company company, CancellationToken cancellationToken = default);
       Task UpdateAsync(Company company, CancellationToken cancellationToken = default);
   }
   ```

2. Implement it in `Infrastructure/Repositories/CompanyRepository.cs`, injecting
   `Context`. Use `AsNoTracking()` for reads, project with
   `.Select(...)` when you only need a subset, and keep private helpers private:

   ```csharp
   private IQueryable<Company> BuildReadQuery() => _dbContext.Companies.AsNoTracking();
   ```

3. Register it in `InfrastructureServiceCollectionExtensions.AddRepositories`:

   ```csharp
   services.AddScoped<ICompanyRepository, CompanyRepository>();
   ```

4. Add `(typeof(CompanyRepository), typeof(ICompanyRepository))` to the
   `ArchitectureRuleTests` theory.

## Adding a new service

1. Declare the contract in `Application/Services/Interfaces/ICompanyService.cs`.
2. Implement it in `Application/Services/Implementations/CompanyService.cs`,
   injecting `ICompanyRepository`, `ICurrentUserService`, `IAuthorizationService`
   and anything else it needs **as interfaces**. Never `Context`.
3. Register it in `ApplicationServiceCollectionExtensions.AddApplicationServices`:

   ```csharp
   services.AddScoped<ICompanyService, CompanyService>();
   ```

4. Add `(typeof(CompanyService), typeof(ICompanyService))` to the
   `ArchitectureRuleTests` theory.

## Adding a new feature

Full checklist, using `Company` as the example. Note the order: **the database
comes first**, and steps 1–2 record what already exists rather than design it.

1. **The table exists already.** Create it by hand in SQL Server, then record it
   in `docs/database/schema/dbo.Company.sql` with a dated `CHANGELOG.md` entry.
   Nothing below can be written until this is done — the mapping is written
   against the real columns.
2. **Entity** — `Application/Models/Entities/Company.cs`, deriving from
   `AuditableEntity`, with properties matching the real columns and their
   nullability exactly.
3. **EF configuration** — `Infrastructure/Data/Configurations/CompanyConfiguration.cs`
   (`IEntityTypeConfiguration<Company>`). Picked up automatically by assembly
   scanning. Map real column names and SQL types; declare no indexes — the
   database owns those.
4. **DbSet** — `public DbSet<Company> Companies { get; set; } = null!;` on
   `Context`.
5. **DTOs** — `Application/DTOs/Companies/`.
6. **Validators** — `Application/Validators/Companies/` (payload shape only).
7. **Repository** — interface + implementation, as above.
8. **Service** — interface + implementation, as above.
9. **Mapping** — `Application/Common/Mapping/CompanyMappings.cs`.
10. **Permissions** — add to `Shared/Constants/Permissions.cs` and to the matrix
    in `AuthorizationService`.
11. **Cache keys** — if you cache, add a `Shared/Constants/CacheKeys.cs` with
    per-user key prefixes, and invalidate on every write path. Nothing is cached
    today, so that file does not currently exist.
12. **Controller** — `Api/Controllers/CompanyController.cs`, thin, injecting
    `ICompanyService` only.
13. **Tests** — service unit tests, validator tests, integration tests (see
    [Writing a new integration test](#writing-a-new-integration-test)), plus the
    two `ArchitectureRuleTests` entries.
14. **Verify** — `dotnet build && ./scripts/run-all-tests.sh`.

For a stored procedure instead of a LINQ query: record it in
`docs/database/procedures/`, add a **keyless** result type under
`Application/Models/Results/` mapped with `HasNoKey()` + `ToView(null)`, and call
it from the repository with `FromSqlInterpolated`.

---

## Security notes

- No secrets in source control. `appsettings.json` ships empty values for the
  connection string and JWT secret; use user secrets locally and a secret store
  in every deployed environment.
- Full JWT validation, configurable expiry, zero default clock skew.
- Passwords, hashes, tokens, secrets and connection strings are never logged.
- All queries go through EF Core and are parameterised; search uses
  `EF.Functions.Like` rather than string interpolation.
- Authorization is centralised and fails closed for unknown roles.
- Deny-by-default authorization means a new controller is protected even if
  `[Authorize]` is forgotten.
- HTTPS redirection and HSTS outside development; CORS restricted to configured
  origins.
- EF entities are never returned from a controller — only DTOs.
- Stack traces are never exposed in a production response.
