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

    /// <summary>
    /// The contents of the <c>dbo.DayMaster</c> lookup table.
    /// <para>
    /// The table is fixed reference data - seven rows, inserted with the schema,
    /// never written by the API - so its ids and codes are safe to declare as
    /// constants. Read the table when you need the rows themselves; use these
    /// when you need to name one day.
    /// </para>
    /// </summary>
    public static class DayMaster
    {
        /// <summary>
        /// Values of the <c>dbo.DayMaster.DayID</c> column - ISO-8601
        /// numbering, Monday first.
        /// <para>
        /// <b>These are not <see cref="System.DayOfWeek"/> values.</b> That enum
        /// numbers Sunday 0 and Saturday 6, so a cast between the two is wrong
        /// for every day of the week. Translate deliberately.
        /// </para>
        /// <para>
        /// They are persisted as the primary key and are referenced by other
        /// rows, so they can never be renumbered.
        /// </para>
        /// </summary>
        public static class DayId
        {
            /// <summary>Stored as <c>1</c>.</summary>
            public const int Monday = 1;

            /// <summary>Stored as <c>2</c>.</summary>
            public const int Tuesday = 2;

            /// <summary>Stored as <c>3</c>.</summary>
            public const int Wednesday = 3;

            /// <summary>Stored as <c>4</c>.</summary>
            public const int Thursday = 4;

            /// <summary>Stored as <c>5</c>.</summary>
            public const int Friday = 5;

            /// <summary>Stored as <c>6</c>.</summary>
            public const int Saturday = 6;

            /// <summary>Stored as <c>7</c>.</summary>
            public const int Sunday = 7;
        }

        /// <summary>
        /// Values of the <c>dbo.DayMaster.DayCode</c> column - the two-letter
        /// codes, stored as <c>varchar(2)</c> and therefore unpadded.
        /// <para>
        /// <c>dbo.TimesheetMasterSetup</c> held codes like these in its
        /// <c>StartDay</c>, <c>EndDay</c> and <c>Exceptionday</c> columns until
        /// 2026-09-17; those columns are <c>int</c> now and reference
        /// <see cref="DayId"/> instead. Nothing in the application matches on a
        /// code any more - these exist for reading and writing
        /// <c>dbo.DayMaster</c> itself.
        /// </para>
        /// </summary>
        public static class DayCode
        {
            /// <summary>Stored as <c>MO</c>.</summary>
            public const string Monday = "MO";

            /// <summary>Stored as <c>TU</c>.</summary>
            public const string Tuesday = "TU";

            /// <summary>Stored as <c>WE</c>.</summary>
            public const string Wednesday = "WE";

            /// <summary>Stored as <c>TH</c>.</summary>
            public const string Thursday = "TH";

            /// <summary>Stored as <c>FR</c>.</summary>
            public const string Friday = "FR";

            /// <summary>Stored as <c>SA</c>.</summary>
            public const string Saturday = "SA";

            /// <summary>Stored as <c>SU</c>.</summary>
            public const string Sunday = "SU";
        }

        /// <summary>
        /// Values of the <c>dbo.DayMaster.Day</c> column - the full English
        /// names, each unique in the table.
        /// </summary>
        public static class DayName
        {
            /// <summary>Stored as <c>Monday</c>.</summary>
            public const string Monday = "Monday";

            /// <summary>Stored as <c>Tuesday</c>.</summary>
            public const string Tuesday = "Tuesday";

            /// <summary>Stored as <c>Wednesday</c>.</summary>
            public const string Wednesday = "Wednesday";

            /// <summary>Stored as <c>Thursday</c>.</summary>
            public const string Thursday = "Thursday";

            /// <summary>Stored as <c>Friday</c>.</summary>
            public const string Friday = "Friday";

            /// <summary>Stored as <c>Saturday</c>.</summary>
            public const string Saturday = "Saturday";

            /// <summary>Stored as <c>Sunday</c>.</summary>
            public const string Sunday = "Sunday";
        }
    }
}
