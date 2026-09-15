namespace ETimeSheet.Shared.Utilities;

/// <summary>
/// Literal values that are persisted or shared across the application and must
/// never be typed inline at a call site.
/// <para>
/// <b>Convention:</b> one nested class per database table, named after that
/// table, and inside it one nested class per coded column. A column called
/// <c>Status</c> means something different on every table, so its values are
/// never declared globally - <c>Constants.TimeLog.Status.Draft</c> can never be
/// confused with, say, <c>Constants.Project.Status.Draft</c>, and neither can be
/// reached by accident from the other's code.
/// </para>
/// </summary>
public static class Constants
{
    /// <summary>Coded column values for the <c>dbo.TimeLog</c> table.</summary>
    public static class TimeLog
    {
        /// <summary>
        /// Values of the <c>dbo.TimeLog.Status</c> column.
        /// <para>
        /// <b>1 = Save, 2 = Draft only.</b> No other value is in use, and this
        /// meaning applies to <c>dbo.TimeLog</c> alone.
        /// </para>
        /// <para>
        /// These are persisted and are read by
        /// <c>spc_GetTimeLoggedDetailsForTask</c>, so they can never be
        /// renumbered: changing one here without rewriting every existing row
        /// would silently relabel history.
        /// <see cref="ETimeSheet.Shared.Enums.TimeLogStatus"/> is defined from
        /// these constants so the two cannot drift apart.
        /// </para>
        /// </summary>
        public static class Status
        {
            /// <summary>A saved entry. Stored as <c>1</c>.</summary>
            public const int Save = 1;

            /// <summary>A draft entry. Stored as <c>2</c>.</summary>
            public const int Draft = 2;
        }
    }
}
