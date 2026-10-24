/*
    Procedure: dbo.spc_GetTimesheetUserDetailsByUserID
    Status:    DRAFT for review - not applied to any database.
    Recorded:  2026-10-24 - DRAFT: new procedure.
    Revised:   2026-10-24 - DRAFT: dbo.Timesheet_Users_Project_setup is now
               the driving table, with dbo.Signup joined to it for the user's
               name, email and other basic details rather than the other way
               round. See "Result" for what that changes.
    Revised:   2026-10-24 - DRAFT: the ORGANISATION is now the project's -
               dbo.TimesheetProject.OrganizationID, reached through the
               assignment's ProjectID - not dbo.Signup.OrganizationID. The
               client was already the project's. Nothing hangs off dbo.Signup
               now except the user's own country and its currency, which no
               project can supply.
    Revised:   2026-10-24 - DRAFT: country and currency split in two. The
               USER's CountryID, CountryName and currency are the user's own
               again - dbo.Signup.CountryID, dbo.Country, and that country's
               dbo.CountryCurrency. Each PROJECT gets its own
               ProjectCountryID, ProjectCountryName and ProjectCurrency* -
               dbo.TimesheetProject.CurrencyCode in dbo.CountryCurrency, and
               that currency's country.
    Revised:   2026-10-24 - DRAFT: ClientCurrencyID, ClientCurrencyName and
               ClientCurrencySymbol dropped - each project shows one currency,
               its own - and the join to dbo.CountryCurrency for them with it.

    Returns one timesheet user - their basic details, and every live project
    assignment with the project, its client and the hours logged on it - for
    GET api/v1/TimesheetProject/get-timesheet-user-details-by-userID/{userID}.

    The ASSIGNMENT is the subject: the procedure reads from
    dbo.Timesheet_Users_Project_setup, which is what makes somebody a
    timesheet user, and joins dbo.Signup only to put a name, an email and the
    rest of their basic details on each row.

    Parameters:
      @PUserID  the user (dbo.Signup.UserID).
      @PToday   the user's today, in the time zone of their timesheet setup -
                worked out by the API, so the current timesheet period is the
                one the user is actually in.

    Result: ONE result set, one row per live ASSIGNMENT
      The user's columns repeat on every row.

      NO ROWS means the user has no live assignment - including a user who
      exists in dbo.Signup but has never been put on a project, and one whose
      every assignment has been removed. A missing or deleted user also gets
      no rows. The three are not distinguishable from the result alone.

      This changed on 2026-10-24 with the driving table. Before it, a live
      user with no live assignment got one row whose assignment, project and
      client columns were all NULL - so the caller could tell "user exists,
      on no project" from "no such user". It no longer can.

      User:
        UserID, Name, Email, Address, ProfilePicture, OrganizationID,
        OrganizationName, CountryID, CountryName, CurrencyID, CurrencyName,
        CurrencySymbol, TimeZone (the user's live dbo.TimesheetMasterSetup,
        lowest SetupID).

        CountryID is dbo.Signup.CountryID and CountryName its dbo.Country
        name. The currency is that country's: the lowest CurrencyID in
        dbo.CountryCurrency for it, as spc_AddEditClientSetup does. NULL when
        the user has no country, or it has no currency.

        OrganizationID and OrganizationName come from the ASSIGNMENT'S
        PROJECT - dbo.TimesheetProject.OrganizationID and dbo.Organization -
        never from dbo.Signup.OrganizationID. A user belongs to one
        organisation, so the pair repeats unchanged on every row and the API
        reads it from the first. They are NULL on a row whose ProjectID names
        no project, or whose project has no organisation.

        The time zone keys off the assignment's UserID.

      Assignment and project:
        TimesheetUserProjectSetupID, ProjectID, ProjectCode, ProjectName,
        ProjectStatus, ProjectStartDate, ProjectEndDate, SubmissionFrequency,
        ProjectCurrencyID, ProjectCurrencyName, ProjectCurrencySymbol,
        ProjectCountryID, ProjectCountryName, ProjectRoleID, ProjectRoleName,
        JobType, CostRate, BillRate, MaxLogHoursPerDay.

        The project's currency is dbo.TimesheetProject.CurrencyCode in
        dbo.CountryCurrency, and its country that currency's CountryID in
        dbo.Country. NULL when the project has no currency, or names one that
        does not exist.

      Hours:
        TotalExpectedHours        dbo.TimesheetProject.Totalhours - the
                                  project's budgeted hours, the same for every
                                  user on it.
        TotalLoggedHours          the user's live hours on the project, all
                                  time (0 when none).
        CurrentPeriodStartDate,   the project's submission period containing
        CurrentPeriodEndDate      @PToday. NULL when the project has no start
                                  date or frequency, has not started, or has
                                  ended.
        CurrentPeriodLoggedHours  the user's live hours on the project within
                                  that period (0 when none; NULL with no
                                  period).
        CurrentSheetSubmissionCode  the period's code: its submitted sheet's,
                                  else the one its time log carries; NULL when
                                  neither exists yet.
        CurrentSheetStatusID      the period's sheet status - 1 Submitted,
                                  2 Approved, 3 Rejected - or NULL when it has
                                  not been submitted.

      Client (the project's, dbo.TimesheetClientSetup; one per project):
        ClientID, ClientCode, ClientName, ClientBillingEmail,
        ClientPhoneNumber, ClientBillingAddress. NULL when the client is
        unknown or deleted. No client currency: the project's own currency is
        the one shown for the project.

    Periods:
      Counted from the project's StartDate, exactly as the API counts them
      (TimesheetProjectService.SubmissionPeriodOf): 7-day blocks for 1
      (weekly), 15-day blocks for 2, and calendar months from the start day
      for 3 (monthly), each counted from the start date itself. DATEADD(month)
      clamps to the month's last day as .NET AddMonths does. Change one, change
      the other.

    Live rows only:
      - The user: dbo.Signup.IsDelete = 0.
      - The assignment: ISNULL(IsDelete, 0) = 0.
      - The role: ISNULL(IsDelete, 0) = 0; the client: IsDeleted = 0.
      - Time log: IsDelete <> 1. Submission: IsDelete <> 1.

    Order:
      By project name, then TimesheetUserProjectSetupID.

    The tables are recorded at:
      ../schema/dbo.Timesheet_Users_Project_setup.sql
      ../schema/dbo.TimesheetProject.sql
      ../schema/dbo.TimesheetUserRole.sql
      ../schema/dbo.Timesheetclientsetup.sql
      ../schema/dbo.TimesheetProjectTimeLog.sql
      ../schema/dbo.TimesheetProjectSubmission.sql
      ../schema/dbo.TimesheetMasterSetup.sql
      ../schema/dbo.Country.sql
      ../schema/dbo.CountryCurrency.sql
    NOT RECORDED - dbo.Signup and dbo.Organization; the test container builds
    them from the EF mapping.
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_GetTimesheetUserDetailsByUserID]
    @PUserID INT,
    @PToday  DATE
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        -- The user
        s.UserID                            AS UserID,
        s.Name                              AS Name,
        s.Email                             AS Email,
        s.Address                           AS Address,
        s.ProfilePicture                    AS ProfilePicture,
        tp.OrganizationID                   AS OrganizationID,
        o.OrganizationName                  AS OrganizationName,
        s.CountryID                         AS CountryID,
        c.Name                              AS CountryName,
        uc.CurrencyID                       AS CurrencyID,
        uc.CurrencyName                     AS CurrencyName,
        uc.CurrencySymbol                   AS CurrencySymbol,
        ms.TimeZone                         AS TimeZone,

        -- The assignment and its project
        tups.TimesheetUserProjectSetupID    AS TimesheetUserProjectSetupID,
        tups.ProjectID                      AS ProjectID,
        tp.Code                             AS ProjectCode,
        tp.Projectname                      AS ProjectName,
        tp.Status                           AS ProjectStatus,
        tp.StartDate                        AS ProjectStartDate,
        tp.EndDate                          AS ProjectEndDate,
        tp.SubmissionFrequency              AS SubmissionFrequency,
        pcur.CurrencyID                     AS ProjectCurrencyID,
        pcur.CurrencyName                   AS ProjectCurrencyName,
        pcur.CurrencySymbol                 AS ProjectCurrencySymbol,
        pcur.CountryID                      AS ProjectCountryID,
        pc.Name                             AS ProjectCountryName,
        tups.ProjectRoleID                  AS ProjectRoleID,
        tur.Rolename                        AS ProjectRoleName,
        tups.Jobtype                        AS JobType,
        tups.Cost_rate_per_hour             AS CostRate,
        tups.Billable_rate_per_hour         AS BillRate,
        tups.Max_log_hour_day               AS MaxLogHoursPerDay,

        -- The hours
        tp.Totalhours                       AS TotalExpectedHours,
        ISNULL(total.Hours, 0)              AS TotalLoggedHours,
        period.PeriodStart                  AS CurrentPeriodStartDate,
        period.PeriodEnd                    AS CurrentPeriodEndDate,
        CASE WHEN period.PeriodStart IS NULL THEN NULL
             ELSE ISNULL(currentHours.Hours, 0) END          AS CurrentPeriodLoggedHours,
        COALESCE(sheet.SheetSubmissionCode, periodCode.SheetSubmissionCode)
                                            AS CurrentSheetSubmissionCode,
        sheet.StatusID                      AS CurrentSheetStatusID,

        -- The project's client
        tcs.ClientID                        AS ClientID,
        tcs.ClientCode                      AS ClientCode,
        tcs.Clientname                      AS ClientName,
        tcs.BillingEmail                    AS ClientBillingEmail,
        tcs.PhoneNumber                     AS ClientPhoneNumber,
        tcs.BillingAddress                  AS ClientBillingAddress
    -- The assignment drives: one row per live assignment of the user.
    FROM dbo.Timesheet_Users_Project_setup tups
    -- Joined for the user's name, email and basic details only. INNER, so a
    -- deleted user's assignments are not returned either.
    INNER JOIN dbo.Signup s
        ON s.UserID = tups.UserID
       AND s.IsDelete = 0
    -- The USER's country, from dbo.Signup.
    LEFT JOIN dbo.Country c
        ON c.ID = s.CountryID
    -- The user's country's currency: the lowest CurrencyID, should it have several.
    OUTER APPLY
    (
        SELECT TOP (1) currency.CurrencyID, currency.CurrencyName, currency.CurrencySymbol
        FROM dbo.CountryCurrency currency
        WHERE currency.CountryID = s.CountryID
        ORDER BY currency.CurrencyID
    ) uc
    -- The user's live timesheet setup, should they have several.
    OUTER APPLY
    (
        SELECT TOP (1) setup.TimeZone
        FROM dbo.TimesheetMasterSetup setup
        WHERE setup.UserID = tups.UserID
          AND ISNULL(setup.IsDelete, 0) = 0
        ORDER BY setup.SetupID
    ) ms
    LEFT JOIN dbo.TimesheetProject tp
        ON tp.ProjectID = tups.ProjectID
    -- The organisation is the PROJECT's, not dbo.Signup's. A user belongs to
    -- one organisation, so it repeats unchanged on every row.
    LEFT JOIN dbo.Organization o
        ON o.OrganizationID = tp.OrganizationID
    -- The PROJECT's currency, and through it the project's country.
    LEFT JOIN dbo.CountryCurrency pcur
        ON pcur.CurrencyID = tp.CurrencyCode
    LEFT JOIN dbo.Country pc
        ON pc.ID = pcur.CountryID
    LEFT JOIN dbo.TimesheetUserRole tur
        ON tur.RoleID = tups.ProjectRoleID
       AND ISNULL(tur.IsDelete, 0) = 0
    LEFT JOIN dbo.TimesheetClientSetup tcs
        ON tcs.ClientID = tp.ClientID
       AND tcs.IsDeleted = 0
    -- All the user's live hours on the project.
    OUTER APPLY
    (
        SELECT SUM(tl.HoursLogged) AS Hours
        FROM dbo.TimesheetProjectTimeLog tl
        WHERE tl.UserID = tups.UserID
          AND tl.ProjectID = tups.ProjectID
          AND tl.IsDelete <> 1
    ) total
    -- The project's start day and frequency, when today is inside the
    -- project: no row otherwise, and so no current period.
    OUTER APPLY
    (
        SELECT
            CAST(tp.StartDate AS DATE)                                AS StartDay,
            CASE tp.SubmissionFrequency WHEN 1 THEN 7 ELSE 15 END     AS BlockDays
        WHERE tp.StartDate IS NOT NULL
          AND tp.SubmissionFrequency IN (1, 2, 3)
          AND CAST(tp.StartDate AS DATE) <= @PToday
          AND (tp.EndDate IS NULL OR CAST(tp.EndDate AS DATE) >= @PToday)
    ) projectStart
    -- Whole months from the start day to today, for a monthly project: one
    -- fewer when today falls before the start day's date in this month.
    OUTER APPLY
    (
        SELECT DATEDIFF(MONTH, projectStart.StartDay, @PToday)
             - CASE WHEN DATEADD(MONTH, DATEDIFF(MONTH, projectStart.StartDay, @PToday), projectStart.StartDay) > @PToday
                    THEN 1 ELSE 0 END                                 AS Months
    ) monthsIn
    -- The submission period containing today, both dates inclusive.
    OUTER APPLY
    (
        SELECT
            CASE WHEN tp.SubmissionFrequency = 3
                 THEN DATEADD(MONTH, monthsIn.Months, projectStart.StartDay)
                 ELSE DATEADD(DAY,
                              DATEDIFF(DAY, projectStart.StartDay, @PToday) / projectStart.BlockDays * projectStart.BlockDays,
                              projectStart.StartDay)
            END                                                       AS PeriodStart,
            CASE WHEN tp.SubmissionFrequency = 3
                 THEN DATEADD(DAY, -1, DATEADD(MONTH, monthsIn.Months + 1, projectStart.StartDay))
                 ELSE DATEADD(DAY,
                              DATEDIFF(DAY, projectStart.StartDay, @PToday) / projectStart.BlockDays * projectStart.BlockDays
                                  + projectStart.BlockDays - 1,
                              projectStart.StartDay)
            END                                                       AS PeriodEnd
        WHERE projectStart.StartDay IS NOT NULL
    ) period
    -- The user's live hours on the project within the current period.
    OUTER APPLY
    (
        SELECT SUM(tl.HoursLogged) AS Hours
        FROM dbo.TimesheetProjectTimeLog tl
        WHERE tl.UserID = tups.UserID
          AND tl.ProjectID = tups.ProjectID
          AND tl.LogDate >= period.PeriodStart
          AND tl.LogDate <= period.PeriodEnd
          AND tl.IsDelete <> 1
    ) currentHours
    -- The current period's submitted sheet, if any.
    OUTER APPLY
    (
        SELECT TOP (1) sub.SheetSubmissionCode, sub.StatusID
        FROM dbo.TimesheetProjectSubmission sub
        WHERE sub.UserID = tups.UserID
          AND sub.ProjectID = tups.ProjectID
          AND CAST(sub.PeriodStartDate AS DATE) = period.PeriodStart
          AND sub.IsDelete <> 1
        ORDER BY sub.TimesheetProjectSubmissionID
    ) sheet
    -- Else the code the period's time log carries - deleted days included,
    -- as the API reuses it.
    OUTER APPLY
    (
        SELECT TOP (1) tl.SheetSubmissionCode
        FROM dbo.TimesheetProjectTimeLog tl
        WHERE tl.UserID = tups.UserID
          AND tl.ProjectID = tups.ProjectID
          AND tl.LogDate >= period.PeriodStart
          AND tl.LogDate <= period.PeriodEnd
          AND tl.SheetSubmissionCode <> N''
        ORDER BY tl.TimesheetProjectTimeLogID
    ) periodCode
    WHERE tups.UserID = @PUserID
      AND ISNULL(tups.IsDelete, 0) = 0
    ORDER BY tp.Projectname, tups.TimesheetUserProjectSetupID;
END
GO
