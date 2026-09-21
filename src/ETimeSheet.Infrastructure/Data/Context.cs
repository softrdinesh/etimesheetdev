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
    /// The <c>dbo.DayMaster</c> lookup table - the seven days of the week.
    /// <b>Read-only:</b> its rows are fixed reference data, so nothing adds,
    /// edits or deletes one through this context.
    /// </summary>
    public DbSet<DayMaster> DayMaster { get; set; } = null!;

    /// <summary>
    /// The <c>dbo.Country</c> lookup table. <b>Read-only:</b> its rows,
    /// including the comma-separated <c>TimeZone</c> column, are maintained by
    /// hand in SQL Server, so nothing adds, edits or deletes one through this
    /// context.
    /// </summary>
    public DbSet<Country> Country { get; set; } = null!;

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
    /// one user logged against one task. The procedure filters out deleted rows.
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
    }
}
