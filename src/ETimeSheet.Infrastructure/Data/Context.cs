using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace ETimeSheet.Infrastructure.Data;

/// <summary>
/// The only Entity Framework Core context in the solution and the only type
/// permitted to talk to SQL Server.
/// <para>
/// This file is the catalogue of everything the application touches in the
/// database: every table and every stored procedure. If it is not declared
/// here, the application cannot reach it.
/// </para>
/// <para>
/// <b>Naming rule: every property is named exactly as the database object it
/// represents</b> - a table's <c>DbSet</c> carries the table name, a stored
/// procedure's <c>DbSet</c> carries the procedure name, <c>spc_</c> prefix and
/// all. This deliberately breaks C# casing conventions so that the mapping
/// between code and <c>dbo</c> is one-to-one and impossible to misread: what
/// you see at the call site is the object that will appear in a SQL trace.
/// </para>
/// <para>
/// Access is restricted to repositories: no controller and no application
/// service may take a dependency on this type.
/// </para>
/// </summary>
public class Context : DbContext
{
    public Context(DbContextOptions<Context> options)
        : base(options)
    {
    }

    // =====================================================================
    // TABLES  -  property name == table name
    //
    // "= null!" is required, not decorative: nullable reference types are on
    // and CS8618 is escalated to an error in Directory.Build.props, so a plain
    // { get; set; } will not compile. EF Core assigns these on construction.
    // =====================================================================

    /// <summary>The <c>dbo.TimeLog</c> table.</summary>
    public DbSet<TimeLog> TimeLog { get; set; } = null!;

    /// <summary>The <c>dbo.TimesheetMasterSetup</c> table.</summary>
    public DbSet<TimesheetMasterSetup> TimesheetMasterSetup { get; set; } = null!;

    /// <summary>
    /// The <c>dbo.DayMaster_Lookup</c> lookup table - the seven days of the week.
    /// <b>Read-only:</b> its rows are fixed reference data, so nothing adds,
    /// edits or deletes one through this context.
    /// </summary>
    public DbSet<DayMaster> DayMaster_Lookup { get; set; } = null!;

    /// <summary>
    /// The <c>dbo.Country</c> lookup table. <b>Read-only:</b> its rows,
    /// including the comma-separated <c>TimeZone</c> column, are maintained by
    /// hand in SQL Server, so nothing adds, edits or deletes one through this
    /// context.
    /// </summary>
    public DbSet<Country> Country { get; set; } = null!;

    /// <summary>The <c>dbo.TimesheetSubmission</c> table.</summary>
    public DbSet<TimesheetSubmission> TimesheetSubmission { get; set; } = null!;

    /// <summary>The <c>dbo.TimeSheetSubmissionAuditLog</c> table.</summary>
    public DbSet<TimeSheetSubmissionAuditLog> TimeSheetSubmissionAuditLog { get; set; } = null!;

    /// <summary>
    /// The <c>dbo.SchedulerConfiguration</c> table - one schedule per background
    /// job. <b>Read-only:</b> its rows are maintained by hand in SQL Server.
    /// </summary>
    public DbSet<SchedulerConfiguration> SchedulerConfiguration { get; set; } = null!;

    /// <summary>The <c>dbo.EmailQueue</c> table - emails waiting to be sent, and their outcome.</summary>
    public DbSet<EmailQueue> EmailQueue { get; set; } = null!;

    /// <summary>
    /// The <c>dbo.Signup</c> user table, maintained by another application.
    /// Written by <c>spc_Signup_timesheet_user</c>, and through EF Core only by
    /// the timesheet-user edit (<c>Name</c>, <c>Email</c>, <c>UpdatedDate</c>,
    /// <c>UpdateBy</c>). The credential columns are not mapped - see the entity.
    /// </summary>
    public DbSet<Signup> Signup { get; set; } = null!;

    /// <summary>
    /// The <c>dbo.Organization</c> table - the companies. <b>Read-only</b>:
    /// maintained by another application.
    /// </summary>
    public DbSet<Organization> Organization { get; set; } = null!;

    /// <summary>The <c>dbo.TimesheetClientSetup</c> table - the clients.</summary>
    public DbSet<TimesheetClientSetup> TimesheetClientSetup { get; set; } = null!;

    /// <summary>
    /// The <c>dbo.CountryCurrency_Lookup</c> lookup - one currency per country.
    /// <b>Read-only:</b> its rows are maintained by hand in SQL Server.
    /// </summary>
    public DbSet<CountryCurrency> CountryCurrency_Lookup { get; set; } = null!;

    /// <summary>
    /// The <c>dbo.TimesheetUserRole</c> table - the roles defined per
    /// organisation. Rows with <c>IsDelete = 1</c> are hidden by a query filter.
    /// </summary>
    public DbSet<TimesheetUserRole> TimesheetUserRole { get; set; } = null!;

    /// <summary>
    /// The <c>dbo.TimesheetProjectCategory</c> table - the project categories
    /// defined per organisation. Rows with <c>IsDelete = 1</c> are hidden by a
    /// query filter.
    /// </summary>
    public DbSet<TimesheetProjectCategory> TimesheetProjectCategory { get; set; } = null!;

    /// <summary>
    /// The <c>dbo.TimesheetProject</c> table - the projects of each
    /// organisation. No soft delete: a removed project is physically deleted.
    /// </summary>
    public DbSet<TimesheetProject> TimesheetProject { get; set; } = null!;

    /// <summary>
    /// The <c>dbo.TimesheetProjectBudget</c> table - the budget lines of each
    /// project. No soft delete: a removed line is physically deleted.
    /// </summary>
    public DbSet<TimesheetProjectBudget> TimesheetProjectBudget { get; set; } = null!;

    /// <summary>
    /// The <c>dbo.TimesheetProjectStatus</c> table - the project statuses
    /// defined per organisation. Rows with a <c>DeletedDate</c> are hidden by a
    /// query filter.
    /// </summary>
    public DbSet<TimesheetProjectStatus> TimesheetProjectStatus { get; set; } = null!;

    /// <summary>
    /// The <c>dbo.Timesheet_Users_Project_setup</c> table - each user's
    /// assignment to a project. Rows with <c>IsDelete</c> = 1 are hidden by a
    /// query filter.
    /// </summary>
    public DbSet<TimesheetUserProjectSetup> Timesheet_Users_Project_setup { get; set; } = null!;

    /// <summary>
    /// The <c>dbo.TimesheetHolidaysetup</c> table - the holidays of each
    /// organisation. Rows with <c>IsDelete</c> = 1 are hidden by a query filter.
    /// </summary>
    public DbSet<TimesheetHolidaySetup> TimesheetHolidaysetup { get; set; } = null!;

    /// <summary>
    /// The <c>dbo.TimeSheet_Weekend_setup</c> table - the weekend days of each
    /// project. Rows with <c>IsDelete</c> = 1 are hidden by a query filter.
    /// </summary>
    public DbSet<TimesheetWeekendSetup> TimeSheet_Weekend_setup { get; set; } = null!;

    /// <summary>
    /// The <c>dbo.Timesheet_role_assignment</c> table - the modules each user
    /// may use. No soft delete: a removed assignment is physically deleted.
    /// </summary>
    public DbSet<TimesheetRoleAssignment> Timesheet_role_assignment { get; set; } = null!;

    /// <summary>
    /// The <c>dbo.Timesheet_Approver_setup</c> table - the timesheet approvers
    /// of each project. Rows with <c>IsDelete</c> = 1 are hidden by a query
    /// filter.
    /// </summary>
    public DbSet<TimesheetApproverSetup> Timesheet_Approver_setup { get; set; } = null!;

    /// <summary>
    /// The <c>dbo.TimesheetProjectSubmission</c> table - one sheet per user,
    /// project and submission period. Rows with <c>IsDelete</c> = 1 are hidden
    /// by a query filter.
    /// </summary>
    public DbSet<TimesheetProjectSubmission> TimesheetProjectSubmission { get; set; } = null!;

    /// <summary>
    /// The <c>dbo.TimesheetProjectTimeLog</c> table - each user's hours on each
    /// project, one row per calendar day. Rows with <c>IsDelete</c> = 1 are
    /// hidden by a query filter.
    /// </summary>
    public DbSet<TimesheetProjectTimeLog> TimesheetProjectTimeLog { get; set; } = null!;

    /// <summary>
    /// The <c>dbo.TimesheetProjectSubmissionAuditLog</c> table - one row per
    /// submit, approve and reject of a project sheet. Append-only.
    /// </summary>
    public DbSet<TimesheetProjectSubmissionAuditLog> TimesheetProjectSubmissionAuditLog { get; set; } = null!;

    // =====================================================================
    // STORED PROCEDURES  -  property name == procedure name
    //
    // Each is a KEYLESS result set, not a table: nothing is tracked, nothing is
    // written, and no schema is implied. The DbSet exists so EF Core can
    // materialise the rows the procedure returns, and a repository executes it:
    //
    //     await _db.spc_GetTimesheetMasterSetupByUserID
    //              .FromSqlInterpolated($"EXEC dbo.spc_GetTimesheetMasterSetupByUserID @PUserID = {userId}")
    //              .ToListAsync(ct);
    //
    // The interpolated holes become real SqlParameters - never string
    // concatenation, so a value can never be parsed as SQL.
    // =====================================================================

    /// <summary>
    /// Result set of <c>dbo.spc_GetTimeLoggedDetailsForTask</c> - every entry
    /// one user has logged, newest first. The procedure filters out deleted rows.
    /// </summary>
    public DbSet<TimeLoggedDetail> spc_GetTimeLoggedDetailsForTask { get; set; } = null!;

    /// <summary>
    /// Result set of <c>dbo.spc_GetTimesheetMasterSetupByUserID</c> - the
    /// timesheet limits and working-week settings held for one user.
    /// </summary>
    public DbSet<TimesheetMasterSetupDetail> spc_GetTimesheetMasterSetupByUserID { get; set; } = null!;

    /// <summary>
    /// Result set of <c>dbo.spc_GetEmployeeListByPOrgID</c> - every employee in
    /// one organisation, with their contracted weekly time and what they have
    /// logged in the current Monday-Sunday week.
    /// </summary>
    public DbSet<EmployeeListDetail> spc_GetEmployeeListByPOrgID { get; set; } = null!;

    /// <summary>
    /// Result set of <c>dbo.spc_GetUserDashboardSummaryByUserID</c> - one
    /// user's logged time today and this timesheet week, against what their
    /// setup expects.
    /// </summary>
    public DbSet<UserDashboardSummaryDetail> spc_GetUserDashboardSummaryByUserID { get; set; } = null!;

    /// <summary>
    /// Result set of <c>dbo.spc_GetAdminDashboardSummaryByOrgID</c> - one
    /// organisation's head counts and this week's logged and expected time.
    /// </summary>
    public DbSet<AdminDashboardSummaryDetail> spc_GetAdminDashboardSummaryByOrgID { get; set; } = null!;

    /// <summary>
    /// Result set of <c>dbo.spc_GetSubmittedSheetList</c> - the submitted
    /// timesheets in one organisation, for every user or one.
    /// </summary>
    public DbSet<SubmittedSheetDetail> spc_GetSubmittedSheetList { get; set; } = null!;

    /// <summary>
    /// Result set of <c>dbo.spc_GetClientListForMasterAdmin</c> - every live
    /// client in <c>dbo.TimesheetClientSetup</c>, ordered by name.
    /// </summary>
    public DbSet<ClientListDetail> spc_GetClientListForMasterAdmin { get; set; } = null!;

    /// <summary>
    /// Result set of <c>dbo.spc_AddEditClientSetup</c> - the client just added
    /// or edited, or no row when an edit names a missing or deleted client.
    /// </summary>
    public DbSet<ClientSaveDetail> spc_AddEditClientSetup { get; set; } = null!;

    /// <summary>
    /// Result set of <c>dbo.spc_GetUserRoleListByOrgID</c> - the live roles of
    /// one organisation, ordered by name.
    /// </summary>
    public DbSet<UserRoleDetail> spc_GetUserRoleListByOrgID { get; set; } = null!;

    /// <summary>
    /// Result set of <c>dbo.spc_AddEditUserRoleByAdmin</c> - the role just
    /// added or renamed, or no row when an edit names a missing role.
    /// </summary>
    public DbSet<UserRoleSaveDetail> spc_AddEditUserRoleByAdmin { get; set; } = null!;

    /// <summary>
    /// Result set of <c>dbo.spc_GetTimesheetProjectCategory</c> - the live
    /// project categories of one organisation, ordered by name.
    /// </summary>
    public DbSet<ProjectCategoryDetail> spc_GetTimesheetProjectCategory { get; set; } = null!;

    /// <summary>
    /// Result set of <c>dbo.spc_AddEditTimesheetProjectCategory</c> - the
    /// category just added or renamed, or no row when an edit names a missing
    /// category.
    /// </summary>
    public DbSet<ProjectCategorySaveDetail> spc_AddEditTimesheetProjectCategory { get; set; } = null!;

    /// <summary>
    /// Result set of <c>dbo.spc_GetTimesheetProject</c> - the projects of one
    /// organisation, ordered by name.
    /// </summary>
    public DbSet<TimesheetProjectDetail> spc_GetTimesheetProject { get; set; } = null!;

    /// <summary>
    /// Result set of <c>dbo.spc_AddEditTimesheetProject</c> - the project just
    /// added or updated, or no row when an update names a missing project.
    /// </summary>
    public DbSet<TimesheetProjectSaveDetail> spc_AddEditTimesheetProject { get; set; } = null!;

    /// <summary>
    /// Result set of <c>dbo.spc_GetTimesheetProjectStatus</c> - the live
    /// project statuses of one organisation, ordered by id.
    /// </summary>
    public DbSet<TimesheetProjectStatusDetail> spc_GetTimesheetProjectStatus { get; set; } = null!;

    /// <summary>
    /// Result set of <c>dbo.spc_AddEditTimesheetProjectStatus</c> - the status
    /// just added or edited, or no row when an edit names a missing status.
    /// </summary>
    public DbSet<TimesheetProjectStatusSaveDetail> spc_AddEditTimesheetProjectStatus { get; set; } = null!;

    /// <summary>
    /// Result set of <c>dbo.spc_GetTimesheetProjectBudget</c> - the budget
    /// lines of one project, ordered by period start.
    /// </summary>
    public DbSet<TimesheetProjectBudgetDetail> spc_GetTimesheetProjectBudget { get; set; } = null!;

    /// <summary>
    /// Result set of <c>dbo.spc_AddEditTimesheetProjectBudget</c> - the budget
    /// line just added or updated, or no row when an update names a missing line.
    /// </summary>
    public DbSet<TimesheetProjectBudgetSaveDetail> spc_AddEditTimesheetProjectBudget { get; set; } = null!;

    /// <summary>
    /// Result set of <c>dbo.spc_GetTimesheetProjectBudgetByOrgID</c> - the
    /// budget lines of every project of one organisation, ordered by project
    /// name, then period start.
    /// </summary>
    public DbSet<TimesheetProjectBudgetByOrgDetail> spc_GetTimesheetProjectBudgetByOrgID { get; set; } = null!;

    /// <summary>
    /// Result set of <c>dbo.spc_GetOrganizationList</c> - every organisation
    /// in <c>dbo.Organization</c>, ordered by name.
    /// </summary>
    public DbSet<OrganizationListDetail> spc_GetOrganizationList { get; set; } = null!;

    /// <summary>
    /// Result set of <c>dbo.spc_GetTimesheetUserListByOrgID</c> - every live
    /// project assignment of every live user of one organisation, ordered by
    /// user name, then project name.
    /// </summary>
    public DbSet<TimesheetUserListDetail> spc_GetTimesheetUserListByOrgID { get; set; } = null!;

    /// <summary>
    /// Result set of <c>dbo.spc_GetTimesheetUserDetailsByUserID</c> - one
    /// user's details, one row per live project assignment with its project,
    /// hours and client.
    /// </summary>
    public DbSet<TimesheetUserDetailsDetail> spc_GetTimesheetUserDetailsByUserID { get; set; } = null!;

    /// <summary>
    /// Result set of <c>dbo.spc_GetTimesheetHolidaySetup</c> - the live
    /// holidays of an organisation, a project or both, ordered by date.
    /// </summary>
    public DbSet<TimesheetHolidaySetupDetail> spc_GetTimesheetHolidaySetup { get; set; } = null!;

    /// <summary>
    /// Result set of <c>dbo.spc_AddEditTimesheetHolidaySetup</c> - the holiday
    /// just added or edited, or no row when an edit names a missing holiday.
    /// </summary>
    public DbSet<TimesheetHolidaySetupSaveDetail> spc_AddEditTimesheetHolidaySetup { get; set; } = null!;

    /// <summary>
    /// Result set of <c>dbo.spc_GetTimesheetWeekendSetup</c> - the live
    /// weekend setups of an organisation, a project or both.
    /// </summary>
    public DbSet<TimesheetWeekendSetupDetail> spc_GetTimesheetWeekendSetup { get; set; } = null!;

    /// <summary>
    /// Result set of <c>dbo.spc_AddEditTimesheetWeekendSetup</c> - the setup
    /// just added or edited, or no row when an edit names a missing setup.
    /// </summary>
    public DbSet<TimesheetWeekendSetupSaveDetail> spc_AddEditTimesheetWeekendSetup { get; set; } = null!;

    /// <summary>
    /// Result set of <c>dbo.spc_GetUserAccessSetup</c> - the module
    /// assignments of live users of an organisation, a user or both.
    /// </summary>
    public DbSet<TimesheetRoleAssignmentDetail> spc_GetUserAccessSetup { get; set; } = null!;

    /// <summary>
    /// Result set of <c>dbo.spc_AddEditUserAccessSetup</c> - the
    /// assignment just added or edited, or no row when an edit names a missing
    /// assignment.
    /// </summary>
    public DbSet<TimesheetRoleAssignmentSaveDetail> spc_AddEditUserAccessSetup { get; set; } = null!;

    /// <summary>
    /// Result set of <c>dbo.spc_GetTimesheetApproverSetup</c> - the live
    /// approver setups of an organisation, a project or both.
    /// </summary>
    public DbSet<TimesheetApproverSetupDetail> spc_GetTimesheetApproverSetup { get; set; } = null!;

    /// <summary>
    /// Result set of <c>dbo.spc_AddEditTimesheetApproverSetup</c> - the setup
    /// just added or edited, or no row when an edit names a missing setup.
    /// </summary>
    public DbSet<TimesheetApproverSetupSaveDetail> spc_AddEditTimesheetApproverSetup { get; set; } = null!;

    // dbo.spc_GetUsersTaskList has NO DbSet, deliberately. It returns two
    // result sets and EF Core materialises only the first, so
    // TimeLogRepository.GetUserTaskListByUserIdAsync runs it on this context's
    // connection and reads both sets itself.

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Tables: assembly scanning, so adding a new IEntityTypeConfiguration<T>
        // under Data/Configurations is enough to register it.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Context).Assembly);

        ConfigureStoredProcedureResults(modelBuilder);
    }

    /// <summary>
    /// Maps the procedure result sets.
    /// <para>
    /// <c>HasNoKey</c> plus <c>ToView(null)</c> is what tells EF Core "this is a
    /// query result, not a table". The column names are the procedure's SELECT
    /// list - its <b>aliases</b> where it uses them - because that is what the
    /// reader binds to. Change an alias in the procedure and the matching line
    /// here must change with it.
    /// </para>
    /// </summary>
    private static void ConfigureStoredProcedureResults(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TimeLoggedDetail>(detail =>
        {
            detail.HasNoKey();
            detail.ToView(null);

            detail.Property(row => row.SheetId).HasColumnName("SheetID");
            detail.Property(row => row.SheetCode).HasColumnName("SheetCode");
            detail.Property(row => row.Description).HasColumnName("Description");
            detail.Property(row => row.StartDate).HasColumnName("StartDate");
            detail.Property(row => row.StartTime).HasColumnName("StartTime");
            detail.Property(row => row.EndDate).HasColumnName("EndDate");
            detail.Property(row => row.EndTime).HasColumnName("EndTime");

            // 1 = Save, 2 = Draft only - see Constants.TimeLog.Status.
            detail.Property(row => row.Status).HasColumnName("Status").HasConversion<int?>();

            // Computed by the procedure, not stored on the table. Both are null
            // when any of the four date/time columns behind DATEDIFF is null.
            detail.Property(row => row.TotalWorkingHours).HasColumnName("TotalWorkingHours");
            detail.Property(row => row.TotalWorkingMinutes).HasColumnName("TotalWorkingMinutes");

            // Added to the procedure 2026-09-25 and 2026-09-24 respectively.
            detail.Property(row => row.TaskId).HasColumnName("TaskID");
            detail.Property(row => row.IsProjectTask).HasColumnName("IsProjectTask");
        });

        modelBuilder.Entity<TimesheetMasterSetupDetail>(setup =>
        {
            setup.HasNoKey();
            setup.ToView(null);

            setup.Property(row => row.SetupId).HasColumnName("SetupID");

            // Aliased by the procedure: MaxTimeinhrs / MaxTiminmins.
            setup.Property(row => row.MaxTimeLoggedByUserInHours).HasColumnName("MaxTimeLoggedByUserInHours");
            setup.Property(row => row.MaxTimeLoggedByUserInMinutes).HasColumnName("MaxTimeLoggedByUserInMinutes");

            setup.Property(row => row.ContractType).HasColumnName("ContractType");
            setup.Property(row => row.StartDay).HasColumnName("StartDay");
            setup.Property(row => row.EndDay).HasColumnName("EndDay");

            // The column is a SQL "int" holding 0 or 1, not a "bit", so the
            // conversion is required: without it EF Core throws
            // "Unable to cast object of type 'System.Int32' to type
            // 'System.Boolean'" on the first row returned. The property stays a
            // bool so callers get true/false rather than a magic number.
            setup.Property(row => row.CanUserLoggedPreDayTime)
                 .HasColumnName("CanUserLoggedPreDayTime")
                 .HasConversion<int>();

            // Added to the procedure 2026-09-21. CountryID comes from the JOIN
            // to dbo.Signup - the person's country - not from the setup row's
            // own CountryID column.
            setup.Property(row => row.CountryId).HasColumnName("CountryID");
            setup.Property(row => row.TimeZone).HasColumnName("TimeZone");
        });

        modelBuilder.Entity<EmployeeListDetail>(employee =>
        {
            employee.HasNoKey();
            employee.ToView(null);

            employee.Property(row => row.UserId).HasColumnName("UserID");
            employee.Property(row => row.SetupId).HasColumnName("SetupID");
            employee.Property(row => row.Name).HasColumnName("Name");
            employee.Property(row => row.Email).HasColumnName("Email");

            // Computed by the procedure from the setup's working week and daily
            // maximum - none of these four is a column on any table.
            employee.Property(row => row.ExpectedHoursPerWeek).HasColumnName("ExpectedHoursPerWeek");
            employee.Property(row => row.ExpectedMinsPerWeek).HasColumnName("ExpectedMinsPerWeek");
            employee.Property(row => row.ExpectedHoursPerWeekText).HasColumnName("ExpectedHoursPerWeekText");

            employee.Property(row => row.TotalLoggedHoursCurrentWeek).HasColumnName("TotalLoggedHoursCurrentWeek");
            employee.Property(row => row.TotalLoggedMinsCurrentWeek).HasColumnName("TotalLoggedMinsCurrentWeek");
            employee.Property(row => row.TotalLoggedHoursCurrentWeekText).HasColumnName("TotalLoggedHoursCurrentWeekText");

            // The procedure's CASE mixes int literals with a DECIMAL(10, 0)
            // cast, and SQL type precedence makes the whole expression decimal -
            // so the property is decimal too. Mapping it as int would throw on
            // the first row.
            employee.Property(row => row.ProgressOnThisWeek).HasColumnName("ProgressOnThisWeek");

            // Aliased by the procedure: the raw column is ContractType, and the
            // alias ContractType is the spelled-out text beside it.
            employee.Property(row => row.ContractTypeId).HasColumnName("ContractTypeID");
            employee.Property(row => row.ContractType).HasColumnName("ContractType");

            // Added to the procedure 2026-09-21. From the JOIN to dbo.Signup -
            // the employee's own country, not the setup row's CountryID.
            employee.Property(row => row.CountryId).HasColumnName("CountryID");
        });

        modelBuilder.Entity<UserDashboardSummaryDetail>(summary =>
        {
            summary.HasNoKey();
            summary.ToView(null);

            summary.Property(row => row.UserId).HasColumnName("UserID");
            summary.Property(row => row.Name).HasColumnName("Name");
            summary.Property(row => row.WeekStartDate).HasColumnName("WeekStartDate");
            summary.Property(row => row.WeekEndDate).HasColumnName("WeekEndDate");
            summary.Property(row => row.TodayLoggedMinutes).HasColumnName("TodayLoggedMinutes");
            summary.Property(row => row.TodayLoggedText).HasColumnName("TodayLoggedText");
            summary.Property(row => row.WeekLoggedMinutes).HasColumnName("WeekLoggedMinutes");
            summary.Property(row => row.WeekLoggedText).HasColumnName("WeekLoggedText");
            summary.Property(row => row.WeekExpectedMinutes).HasColumnName("WeekExpectedMinutes");
            summary.Property(row => row.WeekExpectedText).HasColumnName("WeekExpectedText");
            summary.Property(row => row.WeekLoggedPercentage).HasColumnName("WeekLoggedPercentage").HasPrecision(5, 2);
            summary.Property(row => row.WeekPendingMinutes).HasColumnName("WeekPendingMinutes");
            summary.Property(row => row.WeekPendingText).HasColumnName("WeekPendingText");
        });

        modelBuilder.Entity<AdminDashboardSummaryDetail>(summary =>
        {
            summary.HasNoKey();
            summary.ToView(null);

            summary.Property(row => row.OrganizationId).HasColumnName("OrganizationID");
            summary.Property(row => row.TotalEmployees).HasColumnName("TotalEmployees");
            summary.Property(row => row.TotalFullTimeEmployees).HasColumnName("TotalFullTimeEmployees");
            summary.Property(row => row.TotalPartTimeEmployees).HasColumnName("TotalPartTimeEmployees");
            summary.Property(row => row.WeekLoggedMinutes).HasColumnName("WeekLoggedMinutes");
            summary.Property(row => row.WeekLoggedText).HasColumnName("WeekLoggedText");
            summary.Property(row => row.WeekExpectedMinutes).HasColumnName("WeekExpectedMinutes");
            summary.Property(row => row.WeekExpectedText).HasColumnName("WeekExpectedText");

            // Static placeholders in the procedure - literal 0 until an
            // approval workflow exists.
            summary.Property(row => row.PendingTimesheets).HasColumnName("PendingTimesheets");
            summary.Property(row => row.ApprovedTimesheets).HasColumnName("ApprovedTimesheets");
        });

        modelBuilder.Entity<SubmittedSheetDetail>(sheet =>
        {
            sheet.HasNoKey();
            sheet.ToView(null);

            sheet.Property(row => row.UserId).HasColumnName("UserID");

            sheet.Property(row => row.Name).HasColumnName("Name");

            sheet.Property(row => row.Email).HasColumnName("Email");
            sheet.Property(row => row.SheetCode).HasColumnName("SheetCode");

            // Computed by the procedure from the sheet's earliest entry and the
            // setup's StartDay / EndDay. Null when the sheet has no live entry.
            sheet.Property(row => row.WeekStartDate).HasColumnName("WeekStartDate");
            sheet.Property(row => row.WeekEndDate).HasColumnName("WeekEndDate");

            sheet.Property(row => row.StartDay).HasColumnName("StartDay");
            sheet.Property(row => row.StartDayName).HasColumnName("StartDayName");
            sheet.Property(row => row.EndDay).HasColumnName("EndDay");
            sheet.Property(row => row.EndDayName).HasColumnName("EndDayName");

            // All three DECIMAL(18, 2) - TotalHours as stored, the other two
            // cast by the procedure.
            sheet.Property(row => row.TotalHoursWorked).HasColumnName("TotalHoursWorked").HasPrecision(18, 2);
            sheet.Property(row => row.TotalHoursExpected).HasColumnName("TotalHoursExpected").HasPrecision(18, 2);
            sheet.Property(row => row.TotalHoursDrift).HasColumnName("TotalHoursDrift").HasPrecision(18, 2);

            // Aliased by the procedure: StatusID AS [Status].
            sheet.Property(row => row.Status).HasColumnName("Status");
            sheet.Property(row => row.StatusText).HasColumnName("StatusText");

            sheet.Property(row => row.SubmittedDate).HasColumnName("SubmittedDate");
        });

        modelBuilder.Entity<ClientListDetail>(client =>
        {
            client.HasNoKey();
            client.ToView(null);

            // The procedure aliases the table's mixed-convention columns
            // (Clientname, payment_terms_days, tax_identifier) to these
            // PascalCase names, and Currencycode to CurrencyID - what it holds.
            // CurrencyName / CurrencySymbol are joined from dbo.CountryCurrency_Lookup.
            client.Property(row => row.ClientId).HasColumnName("ClientID");
            client.Property(row => row.ClientCode).HasColumnName("ClientCode");
            client.Property(row => row.ClientName).HasColumnName("ClientName");
            client.Property(row => row.Avatar).HasColumnName("Avatar");
            client.Property(row => row.BillingEmail).HasColumnName("BillingEmail");
            client.Property(row => row.PhoneNumber).HasColumnName("PhoneNumber");
            client.Property(row => row.AlternatePhoneNumber).HasColumnName("AlternatePhoneNumber");
            client.Property(row => row.AlternateEmail).HasColumnName("AlternateEmail");
            client.Property(row => row.BillingAddress).HasColumnName("BillingAddress");
            client.Property(row => row.PaymentTermsDays).HasColumnName("PaymentTermsDays");
            client.Property(row => row.CurrencyId).HasColumnName("CurrencyID");
            client.Property(row => row.CurrencyName).HasColumnName("CurrencyName");
            client.Property(row => row.CurrencySymbol).HasColumnName("CurrencySymbol");
            client.Property(row => row.CountryId).HasColumnName("CountryID");
            client.Property(row => row.TaxIdentifier).HasColumnName("TaxIdentifier");
            client.Property(row => row.OrganizationId).HasColumnName("OrganizationID");
        });

        modelBuilder.Entity<ClientSaveDetail>(client =>
        {
            client.HasNoKey();
            client.ToView(null);

            // The same aliases as spc_GetClientListForMasterAdmin.
            client.Property(row => row.ClientId).HasColumnName("ClientID");
            client.Property(row => row.ClientCode).HasColumnName("ClientCode");
            client.Property(row => row.ClientName).HasColumnName("ClientName");
            client.Property(row => row.Avatar).HasColumnName("Avatar");
            client.Property(row => row.BillingEmail).HasColumnName("BillingEmail");
            client.Property(row => row.PhoneNumber).HasColumnName("PhoneNumber");
            client.Property(row => row.AlternatePhoneNumber).HasColumnName("AlternatePhoneNumber");
            client.Property(row => row.AlternateEmail).HasColumnName("AlternateEmail");
            client.Property(row => row.BillingAddress).HasColumnName("BillingAddress");
            client.Property(row => row.PaymentTermsDays).HasColumnName("PaymentTermsDays");
            client.Property(row => row.CurrencyId).HasColumnName("CurrencyID");
            client.Property(row => row.CurrencyName).HasColumnName("CurrencyName");
            client.Property(row => row.CurrencySymbol).HasColumnName("CurrencySymbol");
            client.Property(row => row.CountryId).HasColumnName("CountryID");
            client.Property(row => row.TaxIdentifier).HasColumnName("TaxIdentifier");
            client.Property(row => row.OrganizationId).HasColumnName("OrganizationID");
        });

        modelBuilder.Entity<UserRoleDetail>(role =>
        {
            role.HasNoKey();
            role.ToView(null);

            role.Property(row => row.RoleId).HasColumnName("RoleID");

            // Aliased by the procedure: Rolename AS RoleName.
            role.Property(row => row.RoleName).HasColumnName("RoleName");
        });

        modelBuilder.Entity<UserRoleSaveDetail>(role =>
        {
            role.HasNoKey();
            role.ToView(null);

            // The same aliases as spc_GetUserRoleListByOrgID.
            role.Property(row => row.RoleId).HasColumnName("RoleID");
            role.Property(row => row.RoleName).HasColumnName("RoleName");
        });

        modelBuilder.Entity<ProjectCategoryDetail>(category =>
        {
            category.HasNoKey();
            category.ToView(null);

            // Aliased by the procedure: ID AS CategoryID, Categoryname AS CategoryName.
            category.Property(row => row.CategoryId).HasColumnName("CategoryID");
            category.Property(row => row.CategoryName).HasColumnName("CategoryName");
        });

        modelBuilder.Entity<ProjectCategorySaveDetail>(category =>
        {
            category.HasNoKey();
            category.ToView(null);

            // The same aliases as spc_GetTimesheetProjectCategory.
            category.Property(row => row.CategoryId).HasColumnName("CategoryID");
            category.Property(row => row.CategoryName).HasColumnName("CategoryName");
        });

        modelBuilder.Entity<TimesheetProjectDetail>(project =>
        {
            project.HasNoKey();
            project.ToView(null);

            // Aliased by the procedure: Projectname AS ProjectName,
            // Totalhours AS TotalHours, TimesheetProjectStatus.Status AS StatusName.
            project.Property(row => row.ProjectId).HasColumnName("ProjectID");
            project.Property(row => row.Code).HasColumnName("Code");
            project.Property(row => row.ProjectName).HasColumnName("ProjectName");
            project.Property(row => row.TotalBudget).HasColumnName("TotalBudget").HasColumnType("decimal(18,2)");
            project.Property(row => row.TotalHours).HasColumnName("TotalHours").HasColumnType("decimal(18,2)");
            project.Property(row => row.StartDate).HasColumnName("StartDate");
            project.Property(row => row.EndDate).HasColumnName("EndDate");
            project.Property(row => row.CurrencyCode).HasColumnName("CurrencyCode");
            project.Property(row => row.CurrencySymbol).HasColumnName("CurrencySymbol");
            project.Property(row => row.Status).HasColumnName("Status");
            project.Property(row => row.StatusName).HasColumnName("StatusName");
            project.Property(row => row.ClientId).HasColumnName("ClientID");
            project.Property(row => row.ClientName).HasColumnName("ClientName");
            project.Property(row => row.SubmissionFrequency).HasColumnName("SubmissionFrequency");
            project.Property(row => row.SubmissionFrequencyName).HasColumnName("SubmissionFrequencyName");
        });

        modelBuilder.Entity<TimesheetProjectSaveDetail>(project =>
        {
            project.HasNoKey();
            project.ToView(null);

            // The same aliases as spc_GetTimesheetProject.
            project.Property(row => row.ProjectId).HasColumnName("ProjectID");
            project.Property(row => row.Code).HasColumnName("Code");
            project.Property(row => row.ProjectName).HasColumnName("ProjectName");
            project.Property(row => row.TotalBudget).HasColumnName("TotalBudget").HasColumnType("decimal(18,2)");
            project.Property(row => row.TotalHours).HasColumnName("TotalHours").HasColumnType("decimal(18,2)");
            project.Property(row => row.StartDate).HasColumnName("StartDate");
            project.Property(row => row.EndDate).HasColumnName("EndDate");
            project.Property(row => row.CurrencyCode).HasColumnName("CurrencyCode");
            project.Property(row => row.CurrencySymbol).HasColumnName("CurrencySymbol");
            project.Property(row => row.Status).HasColumnName("Status");
            project.Property(row => row.StatusName).HasColumnName("StatusName");
            project.Property(row => row.ClientId).HasColumnName("ClientID");
            project.Property(row => row.ClientName).HasColumnName("ClientName");
            project.Property(row => row.SubmissionFrequency).HasColumnName("SubmissionFrequency");
            project.Property(row => row.SubmissionFrequencyName).HasColumnName("SubmissionFrequencyName");
        });

        modelBuilder.Entity<TimesheetProjectStatusDetail>(status =>
        {
            status.HasNoKey();
            status.ToView(null);

            // Aliased by the procedure: Colorcode AS ColorCode.
            status.Property(row => row.StatusId).HasColumnName("StatusID");
            status.Property(row => row.Status).HasColumnName("Status");
            status.Property(row => row.ColorCode).HasColumnName("ColorCode");
        });

        modelBuilder.Entity<TimesheetProjectStatusSaveDetail>(status =>
        {
            status.HasNoKey();
            status.ToView(null);

            // The same aliases as spc_GetTimesheetProjectStatus.
            status.Property(row => row.StatusId).HasColumnName("StatusID");
            status.Property(row => row.Status).HasColumnName("Status");
            status.Property(row => row.ColorCode).HasColumnName("ColorCode");
        });

        modelBuilder.Entity<TimesheetProjectBudgetDetail>(budget =>
        {
            budget.HasNoKey();
            budget.ToView(null);

            // Aliased by the procedure: Hourlyrate AS HourlyRate, Allocated_amount AS AllocatedAmount.
            budget.Property(row => row.BudgetId).HasColumnName("BudgetID");
            budget.Property(row => row.ProjectCode).HasColumnName("ProjectCode");
            budget.Property(row => row.ProjectCategoryId).HasColumnName("ProjectCategoryID");
            budget.Property(row => row.CategoryName).HasColumnName("CategoryName");
            budget.Property(row => row.AllocatedHours).HasColumnName("AllocatedHours").HasColumnType("decimal(18,2)");
            budget.Property(row => row.HourlyRate).HasColumnName("HourlyRate").HasColumnType("decimal(18,2)");
            budget.Property(row => row.AllocatedAmount).HasColumnName("AllocatedAmount").HasColumnType("decimal(18,2)");
            budget.Property(row => row.PeriodStart).HasColumnName("PeriodStart");
            budget.Property(row => row.PeriodEnd).HasColumnName("PeriodEnd");
        });

        modelBuilder.Entity<TimesheetProjectBudgetSaveDetail>(budget =>
        {
            budget.HasNoKey();
            budget.ToView(null);

            // The same aliases as spc_GetTimesheetProjectBudget.
            budget.Property(row => row.BudgetId).HasColumnName("BudgetID");
            budget.Property(row => row.ProjectCode).HasColumnName("ProjectCode");
            budget.Property(row => row.ProjectCategoryId).HasColumnName("ProjectCategoryID");
            budget.Property(row => row.CategoryName).HasColumnName("CategoryName");
            budget.Property(row => row.AllocatedHours).HasColumnName("AllocatedHours").HasColumnType("decimal(18,2)");
            budget.Property(row => row.HourlyRate).HasColumnName("HourlyRate").HasColumnType("decimal(18,2)");
            budget.Property(row => row.AllocatedAmount).HasColumnName("AllocatedAmount").HasColumnType("decimal(18,2)");
            budget.Property(row => row.PeriodStart).HasColumnName("PeriodStart");
            budget.Property(row => row.PeriodEnd).HasColumnName("PeriodEnd");
        });

        modelBuilder.Entity<TimesheetProjectBudgetByOrgDetail>(budget =>
        {
            budget.HasNoKey();
            budget.ToView(null);

            // Aliased by the procedure: Code AS ProjectCode, Projectname AS ProjectName,
            // Hourlyrate AS HourlyRate, Allocated_amount AS AllocatedAmount.
            budget.Property(row => row.ProjectId).HasColumnName("ProjectID");
            budget.Property(row => row.ProjectCode).HasColumnName("ProjectCode");
            budget.Property(row => row.ProjectName).HasColumnName("ProjectName");
            budget.Property(row => row.BudgetId).HasColumnName("BudgetID");
            budget.Property(row => row.ProjectCategoryId).HasColumnName("ProjectCategoryID");
            budget.Property(row => row.CategoryName).HasColumnName("CategoryName");
            budget.Property(row => row.AllocatedHours).HasColumnName("AllocatedHours").HasColumnType("decimal(18,2)");
            budget.Property(row => row.HourlyRate).HasColumnName("HourlyRate").HasColumnType("decimal(18,2)");
            budget.Property(row => row.AllocatedAmount).HasColumnName("AllocatedAmount").HasColumnType("decimal(18,2)");
            budget.Property(row => row.PeriodStart).HasColumnName("PeriodStart");
            budget.Property(row => row.PeriodEnd).HasColumnName("PeriodEnd");
        });

        modelBuilder.Entity<OrganizationListDetail>(organization =>
        {
            organization.HasNoKey();
            organization.ToView(null);

            organization.Property(row => row.OrganizationId).HasColumnName("OrganizationID");
            organization.Property(row => row.OrganizationName).HasColumnName("OrganizationName");
            organization.Property(row => row.CountryId).HasColumnName("CountryID");
        });

        modelBuilder.Entity<TimesheetUserListDetail>(user =>
        {
            user.HasNoKey();
            user.ToView(null);

            // Aliased by the procedure: Projectname AS ProjectName, Rolename AS
            // ProjectRoleName, Jobtype AS JobType, Cost_rate_per_hour AS CostRate,
            // Billable_rate_per_hour AS BillRate, WorklocationRange AS
            // WorkLocationRange, ProjectlocLatitude / ProjectlocLongtitude AS
            // ProjectLocationLatitude / ProjectLocationLongitude,
            // Max_log_hour_day AS MaxLogHoursPerDay. Country, address and
            // currency are the user's, from dbo.Signup, dbo.Country and
            // dbo.CountryCurrency_Lookup.
            user.Property(row => row.TimesheetUserProjectSetupId).HasColumnName("TimesheetUserProjectSetupID");
            user.Property(row => row.UserId).HasColumnName("UserID");
            user.Property(row => row.Name).HasColumnName("Name");
            user.Property(row => row.Email).HasColumnName("Email");
            user.Property(row => row.OrganizationId).HasColumnName("OrganizationID");
            user.Property(row => row.ProjectId).HasColumnName("ProjectID");
            user.Property(row => row.ProjectCode).HasColumnName("ProjectCode");
            user.Property(row => row.ProjectName).HasColumnName("ProjectName");
            user.Property(row => row.ProjectRoleId).HasColumnName("ProjectRoleID");
            user.Property(row => row.ProjectRoleName).HasColumnName("ProjectRoleName");
            user.Property(row => row.JobType).HasColumnName("JobType");
            user.Property(row => row.CostRate).HasColumnName("CostRate").HasColumnType("decimal(18,2)");
            user.Property(row => row.BillRate).HasColumnName("BillRate").HasColumnType("decimal(18,2)");
            user.Property(row => row.WorkLocationRange).HasColumnName("WorkLocationRange");
            user.Property(row => row.ProjectLocationLatitude).HasColumnName("ProjectLocationLatitude");
            user.Property(row => row.ProjectLocationLongitude).HasColumnName("ProjectLocationLongitude");
            user.Property(row => row.MaxLogHoursPerDay).HasColumnName("MaxLogHoursPerDay");
            user.Property(row => row.CountryId).HasColumnName("CountryID");
            user.Property(row => row.CountryName).HasColumnName("CountryName");
            user.Property(row => row.Address).HasColumnName("Address");
            user.Property(row => row.CurrencyId).HasColumnName("CurrencyID");
            user.Property(row => row.CurrencyName).HasColumnName("CurrencyName");
            user.Property(row => row.CurrencySymbol).HasColumnName("CurrencySymbol");
        });

        modelBuilder.Entity<TimesheetUserDetailsDetail>(details =>
        {
            details.HasNoKey();
            details.ToView(null);

            // One row per assignment, the user's columns repeated; the
            // procedure aliases every column to the name bound here.
            details.Property(row => row.UserId).HasColumnName("UserID");
            details.Property(row => row.Name).HasColumnName("Name");
            details.Property(row => row.Email).HasColumnName("Email");
            details.Property(row => row.Address).HasColumnName("Address");
            details.Property(row => row.ProfilePicture).HasColumnName("ProfilePicture");
            details.Property(row => row.OrganizationId).HasColumnName("OrganizationID");
            details.Property(row => row.OrganizationName).HasColumnName("OrganizationName");
            details.Property(row => row.CountryId).HasColumnName("CountryID");
            details.Property(row => row.CountryName).HasColumnName("CountryName");
            details.Property(row => row.CurrencyId).HasColumnName("CurrencyID");
            details.Property(row => row.CurrencyName).HasColumnName("CurrencyName");
            details.Property(row => row.CurrencySymbol).HasColumnName("CurrencySymbol");
            details.Property(row => row.TimeZone).HasColumnName("TimeZone");
            details.Property(row => row.TimesheetUserProjectSetupId).HasColumnName("TimesheetUserProjectSetupID");
            details.Property(row => row.ProjectId).HasColumnName("ProjectID");
            details.Property(row => row.ProjectCode).HasColumnName("ProjectCode");
            details.Property(row => row.ProjectName).HasColumnName("ProjectName");
            details.Property(row => row.ProjectStatus).HasColumnName("ProjectStatus");
            details.Property(row => row.ProjectStartDate).HasColumnName("ProjectStartDate").HasColumnType("datetime");
            details.Property(row => row.ProjectEndDate).HasColumnName("ProjectEndDate").HasColumnType("datetime");
            details.Property(row => row.SubmissionFrequency).HasColumnName("SubmissionFrequency");
            details.Property(row => row.ProjectCurrencyId).HasColumnName("ProjectCurrencyID");
            details.Property(row => row.ProjectCurrencyName).HasColumnName("ProjectCurrencyName");
            details.Property(row => row.ProjectCurrencySymbol).HasColumnName("ProjectCurrencySymbol");
            details.Property(row => row.ProjectCountryId).HasColumnName("ProjectCountryID");
            details.Property(row => row.ProjectCountryName).HasColumnName("ProjectCountryName");
            details.Property(row => row.ProjectRoleId).HasColumnName("ProjectRoleID");
            details.Property(row => row.ProjectRoleName).HasColumnName("ProjectRoleName");
            details.Property(row => row.JobType).HasColumnName("JobType");
            details.Property(row => row.CostRate).HasColumnName("CostRate").HasColumnType("decimal(18,2)");
            details.Property(row => row.BillRate).HasColumnName("BillRate").HasColumnType("decimal(18,2)");
            details.Property(row => row.MaxLogHoursPerDay).HasColumnName("MaxLogHoursPerDay");
            details.Property(row => row.TotalExpectedHours).HasColumnName("TotalExpectedHours").HasColumnType("decimal(18,2)");
            details.Property(row => row.TotalLoggedHours).HasColumnName("TotalLoggedHours");
            details.Property(row => row.CurrentPeriodStartDate).HasColumnName("CurrentPeriodStartDate").HasColumnType("date");
            details.Property(row => row.CurrentPeriodEndDate).HasColumnName("CurrentPeriodEndDate").HasColumnType("date");
            details.Property(row => row.CurrentPeriodLoggedHours).HasColumnName("CurrentPeriodLoggedHours");
            details.Property(row => row.CurrentSheetSubmissionCode).HasColumnName("CurrentSheetSubmissionCode");
            details.Property(row => row.CurrentSheetStatusId).HasColumnName("CurrentSheetStatusID");
            details.Property(row => row.ClientId).HasColumnName("ClientID");
            details.Property(row => row.ClientCode).HasColumnName("ClientCode");
            details.Property(row => row.ClientName).HasColumnName("ClientName");
            details.Property(row => row.ClientBillingEmail).HasColumnName("ClientBillingEmail");
            details.Property(row => row.ClientPhoneNumber).HasColumnName("ClientPhoneNumber");
            details.Property(row => row.ClientBillingAddress).HasColumnName("ClientBillingAddress");
        });

        modelBuilder.Entity<TimesheetHolidaySetupDetail>(holiday =>
        {
            holiday.HasNoKey();
            holiday.ToView(null);

            // Aliased by the procedure: TimesheetHolidaysetupID AS TimesheetHolidaySetupID,
            // Country.Name AS CountryName, Clientname AS ClientName, Code AS
            // ProjectCode, Projectname AS ProjectName.
            holiday.Property(row => row.TimesheetHolidaySetupId).HasColumnName("TimesheetHolidaySetupID");
            holiday.Property(row => row.HolidayDate).HasColumnName("HolidayDate").HasColumnType("date");
            holiday.Property(row => row.Description).HasColumnName("Description");
            holiday.Property(row => row.OrganizationId).HasColumnName("OrganizationID");
            holiday.Property(row => row.CountryId).HasColumnName("CountryID");
            holiday.Property(row => row.CountryName).HasColumnName("CountryName");
            holiday.Property(row => row.ClientId).HasColumnName("ClientID");
            holiday.Property(row => row.ClientName).HasColumnName("ClientName");
            holiday.Property(row => row.ProjectId).HasColumnName("ProjectID");
            holiday.Property(row => row.ProjectCode).HasColumnName("ProjectCode");
            holiday.Property(row => row.ProjectName).HasColumnName("ProjectName");
            holiday.Property(row => row.CreateDate).HasColumnName("CreateDate").HasColumnType("datetime");
            holiday.Property(row => row.CreatedBy).HasColumnName("CreatedBy");
        });

        modelBuilder.Entity<TimesheetHolidaySetupSaveDetail>(holiday =>
        {
            holiday.HasNoKey();
            holiday.ToView(null);

            // The same aliases as spc_GetTimesheetHolidaySetup.
            holiday.Property(row => row.TimesheetHolidaySetupId).HasColumnName("TimesheetHolidaySetupID");
            holiday.Property(row => row.HolidayDate).HasColumnName("HolidayDate").HasColumnType("date");
            holiday.Property(row => row.Description).HasColumnName("Description");
            holiday.Property(row => row.OrganizationId).HasColumnName("OrganizationID");
            holiday.Property(row => row.CountryId).HasColumnName("CountryID");
            holiday.Property(row => row.CountryName).HasColumnName("CountryName");
            holiday.Property(row => row.ClientId).HasColumnName("ClientID");
            holiday.Property(row => row.ClientName).HasColumnName("ClientName");
            holiday.Property(row => row.ProjectId).HasColumnName("ProjectID");
            holiday.Property(row => row.ProjectCode).HasColumnName("ProjectCode");
            holiday.Property(row => row.ProjectName).HasColumnName("ProjectName");
            holiday.Property(row => row.CreateDate).HasColumnName("CreateDate").HasColumnType("datetime");
            holiday.Property(row => row.CreatedBy).HasColumnName("CreatedBy");
        });

        modelBuilder.Entity<TimesheetWeekendSetupDetail>(setup =>
        {
            setup.HasNoKey();
            setup.ToView(null);

            // Aliased by the procedure: OrgID AS OrganizationID, Clientname AS
            // ClientName, Code AS ProjectCode, Projectname AS ProjectName.
            setup.Property(row => row.TimeSheetWeekendSetupId).HasColumnName("TimeSheetWeekendSetupID");
            setup.Property(row => row.OrganizationId).HasColumnName("OrganizationID");
            setup.Property(row => row.ClientId).HasColumnName("ClientID");
            setup.Property(row => row.ClientName).HasColumnName("ClientName");
            setup.Property(row => row.ProjectId).HasColumnName("ProjectID");
            setup.Property(row => row.ProjectCode).HasColumnName("ProjectCode");
            setup.Property(row => row.ProjectName).HasColumnName("ProjectName");
            setup.Property(row => row.IsMondayWeekend).HasColumnName("IsMondayWeekend");
            setup.Property(row => row.IsTuesdayWeekend).HasColumnName("IsTuesdayWeekend");
            setup.Property(row => row.IsWednesdayWeekend).HasColumnName("IsWednesdayWeekend");
            setup.Property(row => row.IsThursdayWeekend).HasColumnName("IsThursdayWeekend");
            setup.Property(row => row.IsFridayWeekend).HasColumnName("IsFridayWeekend");
            setup.Property(row => row.IsSaturdayWeekend).HasColumnName("IsSaturdayWeekend");
            setup.Property(row => row.IsSundayWeekend).HasColumnName("IsSundayWeekend");
            setup.Property(row => row.CreateDate).HasColumnName("CreateDate").HasColumnType("datetime");
            setup.Property(row => row.CreatedBy).HasColumnName("CreatedBy");
            setup.Property(row => row.UpdatedDate).HasColumnName("UpdatedDate").HasColumnType("datetime");
            setup.Property(row => row.UpdatedBy).HasColumnName("UpdatedBy");
        });

        modelBuilder.Entity<TimesheetWeekendSetupSaveDetail>(setup =>
        {
            setup.HasNoKey();
            setup.ToView(null);

            // The same aliases as spc_GetTimesheetWeekendSetup.
            setup.Property(row => row.TimeSheetWeekendSetupId).HasColumnName("TimeSheetWeekendSetupID");
            setup.Property(row => row.OrganizationId).HasColumnName("OrganizationID");
            setup.Property(row => row.ClientId).HasColumnName("ClientID");
            setup.Property(row => row.ClientName).HasColumnName("ClientName");
            setup.Property(row => row.ProjectId).HasColumnName("ProjectID");
            setup.Property(row => row.ProjectCode).HasColumnName("ProjectCode");
            setup.Property(row => row.ProjectName).HasColumnName("ProjectName");
            setup.Property(row => row.IsMondayWeekend).HasColumnName("IsMondayWeekend");
            setup.Property(row => row.IsTuesdayWeekend).HasColumnName("IsTuesdayWeekend");
            setup.Property(row => row.IsWednesdayWeekend).HasColumnName("IsWednesdayWeekend");
            setup.Property(row => row.IsThursdayWeekend).HasColumnName("IsThursdayWeekend");
            setup.Property(row => row.IsFridayWeekend).HasColumnName("IsFridayWeekend");
            setup.Property(row => row.IsSaturdayWeekend).HasColumnName("IsSaturdayWeekend");
            setup.Property(row => row.IsSundayWeekend).HasColumnName("IsSundayWeekend");
            setup.Property(row => row.CreateDate).HasColumnName("CreateDate").HasColumnType("datetime");
            setup.Property(row => row.CreatedBy).HasColumnName("CreatedBy");
            setup.Property(row => row.UpdatedDate).HasColumnName("UpdatedDate").HasColumnType("datetime");
            setup.Property(row => row.UpdatedBy).HasColumnName("UpdatedBy");
        });

        modelBuilder.Entity<TimesheetRoleAssignmentDetail>(assignment =>
        {
            assignment.HasNoKey();
            assignment.ToView(null);

            // Aliased by the procedure: Signup.Name AS UserName, the Mod_
            // columns AS IsClientSetup .. IsViewTimesheet, Updatedby AS UpdatedBy.
            assignment.Property(row => row.AssignmentId).HasColumnName("AssignmentID");
            assignment.Property(row => row.UserId).HasColumnName("UserID");
            assignment.Property(row => row.UserName).HasColumnName("UserName");
            assignment.Property(row => row.Email).HasColumnName("Email");
            assignment.Property(row => row.OrganizationId).HasColumnName("OrganizationID");
            assignment.Property(row => row.IsClientSetup).HasColumnName("IsClientSetup");
            assignment.Property(row => row.IsProject).HasColumnName("IsProject");
            assignment.Property(row => row.IsProjectBudget).HasColumnName("IsProjectBudget");
            assignment.Property(row => row.IsProjectCategory).HasColumnName("IsProjectCategory");
            assignment.Property(row => row.IsStatusSetup).HasColumnName("IsStatusSetup");
            assignment.Property(row => row.IsRoleSetup).HasColumnName("IsRoleSetup");
            assignment.Property(row => row.IsUserSetup).HasColumnName("IsUserSetup");
            assignment.Property(row => row.IsTimeLog).HasColumnName("IsTimeLog");
            assignment.Property(row => row.IsViewTimesheet).HasColumnName("IsViewTimesheet");
            assignment.Property(row => row.CreateDate).HasColumnName("CreateDate").HasColumnType("datetime");
            assignment.Property(row => row.CreatedBy).HasColumnName("CreatedBy");
            assignment.Property(row => row.UpdatedDate).HasColumnName("UpdatedDate").HasColumnType("datetime");
            assignment.Property(row => row.UpdatedBy).HasColumnName("UpdatedBy");
        });

        modelBuilder.Entity<TimesheetRoleAssignmentSaveDetail>(assignment =>
        {
            assignment.HasNoKey();
            assignment.ToView(null);

            // The same aliases as spc_GetUserAccessSetup.
            assignment.Property(row => row.AssignmentId).HasColumnName("AssignmentID");
            assignment.Property(row => row.UserId).HasColumnName("UserID");
            assignment.Property(row => row.UserName).HasColumnName("UserName");
            assignment.Property(row => row.Email).HasColumnName("Email");
            assignment.Property(row => row.OrganizationId).HasColumnName("OrganizationID");
            assignment.Property(row => row.IsClientSetup).HasColumnName("IsClientSetup");
            assignment.Property(row => row.IsProject).HasColumnName("IsProject");
            assignment.Property(row => row.IsProjectBudget).HasColumnName("IsProjectBudget");
            assignment.Property(row => row.IsProjectCategory).HasColumnName("IsProjectCategory");
            assignment.Property(row => row.IsStatusSetup).HasColumnName("IsStatusSetup");
            assignment.Property(row => row.IsRoleSetup).HasColumnName("IsRoleSetup");
            assignment.Property(row => row.IsUserSetup).HasColumnName("IsUserSetup");
            assignment.Property(row => row.IsTimeLog).HasColumnName("IsTimeLog");
            assignment.Property(row => row.IsViewTimesheet).HasColumnName("IsViewTimesheet");
            assignment.Property(row => row.CreateDate).HasColumnName("CreateDate").HasColumnType("datetime");
            assignment.Property(row => row.CreatedBy).HasColumnName("CreatedBy");
            assignment.Property(row => row.UpdatedDate).HasColumnName("UpdatedDate").HasColumnType("datetime");
            assignment.Property(row => row.UpdatedBy).HasColumnName("UpdatedBy");
        });

        modelBuilder.Entity<TimesheetApproverSetupDetail>(setup =>
        {
            setup.HasNoKey();
            setup.ToView(null);

            // Aliased by the procedure: OrgID AS OrganizationID, Code AS ProjectCode,
            // Projectname AS ProjectName, Signup.Name AS ApproverName, Signup.Email
            // AS ApproverEmail.
            setup.Property(row => row.TimesheetApproverSetupId).HasColumnName("TimesheetApproverSetupID");
            setup.Property(row => row.OrganizationId).HasColumnName("OrganizationID");
            setup.Property(row => row.ProjectId).HasColumnName("ProjectID");
            setup.Property(row => row.ProjectCode).HasColumnName("ProjectCode");
            setup.Property(row => row.ProjectName).HasColumnName("ProjectName");
            setup.Property(row => row.ApproverId).HasColumnName("ApproverID");
            setup.Property(row => row.ApproverName).HasColumnName("ApproverName");
            setup.Property(row => row.ApproverEmail).HasColumnName("ApproverEmail");
            setup.Property(row => row.IsTimesheetApprover).HasColumnName("IsTimesheetApprover");
            setup.Property(row => row.CreateDate).HasColumnName("CreateDate").HasColumnType("datetime");
            setup.Property(row => row.CreatedBy).HasColumnName("CreatedBy");
        });

        modelBuilder.Entity<TimesheetApproverSetupSaveDetail>(setup =>
        {
            setup.HasNoKey();
            setup.ToView(null);

            // The same aliases as spc_GetTimesheetApproverSetup.
            setup.Property(row => row.TimesheetApproverSetupId).HasColumnName("TimesheetApproverSetupID");
            setup.Property(row => row.OrganizationId).HasColumnName("OrganizationID");
            setup.Property(row => row.ProjectId).HasColumnName("ProjectID");
            setup.Property(row => row.ProjectCode).HasColumnName("ProjectCode");
            setup.Property(row => row.ProjectName).HasColumnName("ProjectName");
            setup.Property(row => row.ApproverId).HasColumnName("ApproverID");
            setup.Property(row => row.ApproverName).HasColumnName("ApproverName");
            setup.Property(row => row.ApproverEmail).HasColumnName("ApproverEmail");
            setup.Property(row => row.IsTimesheetApprover).HasColumnName("IsTimesheetApprover");
            setup.Property(row => row.CreateDate).HasColumnName("CreateDate").HasColumnType("datetime");
            setup.Property(row => row.CreatedBy).HasColumnName("CreatedBy");
        });
    }
}
