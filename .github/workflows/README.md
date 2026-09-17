# CI for pull requests

`pr-validation.yml` runs on every pull request into `main`. It builds first,
then runs the tests, in four jobs:

    build               restore + build in Release, warnings as errors
    unit-tests          Category=Unit         (no Docker, no database)
    integration-tests   Category=Integration  (throwaway SQL Server in Docker)
    pr-gate             passes only if all three above passed

## Making a failure actually block the merge

**The workflow alone does not stop anyone merging a red pull request.** GitHub
Actions only reports a result. Refusing the merge is a repository setting, and
it has to be turned on once, by hand:

1. GitHub > the repository > **Settings** > **Branches**
2. **Add branch protection rule** (or edit the existing rule for `main`)
3. Branch name pattern: `main`
4. Tick **Require status checks to pass before merging**
5. Tick **Require branches to be up to date before merging**
6. In the search box, add the check named **`PR gate`**
7. Save

Add **`PR gate`** and nothing else. It already fails when the build, the unit
tests or the integration tests fail, so adding a new job to the workflow never
requires touching these settings again. If you list the three jobs individually
instead, a job added later is not required by anything and cannot block a merge.

The check only appears in that search box after the workflow has run at least
once. If the list is empty, open a pull request (or run the workflow from the
**Actions** tab with **Run workflow**) and come back to the setting afterwards.

## What CI never touches

No job connects to the live PPMUAT database.

- The unit tests use no database at all.
- The integration tests create their own SQL Server container through
  Testcontainers: random port, generated password, dedicated database, destroyed
  when the run ends. `ETimeSheetApiFactory` injects that container's connection
  string over `Database:ConnectionString`, so the live one is unreachable from a
  test even though it is present in `appsettings.json`.
- The container's schema is built by executing the recorded DDL in
  `docs/database/schema` and `docs/database/procedures` - the same files the
  schema history is kept in. No migrations are involved.

## Things worth knowing

**Current coverage is thin.** 40 unit tests, and only two integration classes
(`HealthEndpointTests`, `AuthenticationTests`). Nothing covers the Admin or
TimeLog endpoints yet, so a green tick means "it builds and nothing already
covered broke" - not "the new endpoints work".

**`AuthenticationTests` may fail.** Authentication is switched off across the
project at the moment, and those tests were written when it was on. They cannot
be run on the development Mac (no Docker), so this is unverified. If the first
run is red there, it is a pre-existing condition rather than anything the pull
request changed.

**The SQL Server image tag is written down twice**, in
`SqlServerFixture.DefaultImage` and in the `ETIMESHEET_TEST_SQL_IMAGE` variable
in the workflow. Change one, change the other.

**The shell scripts are not used by CI.** `scripts/run-integration-tests.sh`
tries to start Docker Desktop and handles arm64 emulation - both meaningless on
a Linux runner. The workflow calls `dotnet test` directly with the same filter.
