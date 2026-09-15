namespace ETimeSheet.Shared.Constants;

/// <summary>
/// Permission identifiers used by <c>IAuthorizationService</c>.
/// <para>
/// Permissions - not role ids - are what business code asks about. The mapping
/// from role to permission lives in one place (<c>AuthorizationService</c>) and
/// is intended to move to the database once the role matrix is finalised.
/// </para>
/// <para>
/// Add a permission here when the operation it guards is built, not before: an
/// unused permission looks like a granted capability to anyone reading the matrix.
/// </para>
/// </summary>
public static class Permissions
{
    public static class TimeLogs
    {
        /// <summary>Read time logs belonging to any user, not just your own.</summary>
        public const string ViewAll = "timelogs.view.all";
    }
}
