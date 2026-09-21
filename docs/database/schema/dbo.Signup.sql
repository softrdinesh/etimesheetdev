/*
    Table: dbo.Signup
    Recorded: 2026-09-21

    *** PARTIAL RECORDING - NOT THE LIVE DDL. READ THIS BEFORE TRUSTING IT. ***

    dbo.Signup is the people table: it existed before this API and is owned by
    the database, not by this repository. Its real definition has never been
    handed over, so what follows is NOT a copy of the live table. It records
    only the SIX COLUMNS this codebase can actually see - the ones the two
    recorded procedures select, join or filter on:

        UserID  Name  Email  RoleID  OrganizationID  CountryID

    The live table certainly has more (passwords, dates, status, whatever the
    sign-up flow writes). None of that is reproduced here, because none of it
    has been seen. Types and nullability below are the narrowest thing
    consistent with how the columns are used, not measured facts.

    WHY THE FILE EXISTS AT ALL: dbo.spc_GetEmployeeListByPOrgID and
    dbo.spc_GetTimesheetMasterSetupByUserID both join this table, and the
    integration fixture replays docs/database into a throwaway container. With
    no dbo.Signup in the container, both procedures compile (SQL Server defers
    name resolution) and then fail at run time with
    "Invalid object name 'dbo.Signup'", and ResetDatabaseAsync fails with it
    too. The suite cannot run without this file.

    WHAT TO DO WITH IT: ask the database owner to script the real table
    (SSMS: Script Table as -> CREATE To) and replace everything below with it,
    then add a line to CHANGELOG.md saying the recording was completed. Until
    then, treat a disagreement between this file and production as this file
    being wrong.

    THIS FILE IS NEVER EXECUTED AGAINST A REAL DATABASE. Nothing in the API
    runs it, and it must never be run by hand against PPMUAT or production -
    it would DROP the people table. Its only reader is the integration
    fixture, against a container that is destroyed at the end of the run.
    See ../README.md.

    No entity, deliberately:
      The API never reads or writes this table directly - it reaches the rows
      only through the two procedures - so there is no Signup entity and no
      ISignupRepository. Integration tests describe the row themselves
      (EmployeeListTestData.SignupRow) and insert it with raw SQL
      (ETimeSheetApiFactory.SeedEmployeesAsync), which is also why
      ETimeSheetApiFactory.TablesOutsideTheModel has to name it: it is not in
      the EF model, so the reset script cannot discover it.

    Column notes:
      - UserID is the join key to dbo.TimesheetMasterSetup.UserID and
        dbo.TimeLog.UserID. Recorded as IDENTITY because tests pick the ids
        and have to SET IDENTITY_INSERT dbo.Signup ON to do it; if the live
        column turns out not to be an identity, that seeding breaks and this
        file is what needs correcting.
      - RoleID = 2 is what spc_GetEmployeeListByPOrgID means by "employee".
        That does NOT agree with ETimeSheet.Shared.Enums.RoleType, where 2 is
        Manager. The two vocabularies genuinely differ; nothing reconciles
        them.
      - OrganizationID is the only filter the employee-list endpoint applies,
        so it is the tenant boundary for that read.
      - CountryID is the person's own country, added to both procedures on
        2026-09-21. It is NOT dbo.TimesheetMasterSetup.CountryID, which the
        Admin save writes and resolves the time zone against; the two can
        disagree.
*/

IF OBJECT_ID(N'dbo.Signup', N'U') IS NOT NULL
    DROP TABLE dbo.Signup;
GO

CREATE TABLE dbo.Signup
(
    UserID          int            IDENTITY(1,1) NOT NULL,
    Name            nvarchar(200)  NULL,
    Email           nvarchar(200)  NULL,
    RoleID          int            NULL,
    OrganizationID  int            NULL,
    CountryID       int            NULL,
    CONSTRAINT PK_Signup PRIMARY KEY CLUSTERED (UserID)
);
GO
