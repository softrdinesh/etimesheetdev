using System.Globalization;
using ETimeSheet.Shared.Exceptions;

namespace ETimeSheet.Shared.Utilities;

/// <summary>
/// The single reader for every time of day the API accepts in a payload.
/// <para>
/// <b>Time always arrives as a string in <c>hh:mm:ss</c></b> - <c>"09:00:00"</c>,
/// never <c>9</c>, never <c>"9:00"</c>, never <c>"09:00:00.0000000"</c> and never
/// a JSON number of hours. One format, one parser, so two endpoints cannot end
/// up disagreeing about what <c>"9"</c> meant.
/// </para>
/// <para>
/// A DTO therefore declares the property as <see cref="string"/> and the service
/// calls <see cref="Parse"/> or <see cref="ParseOptional"/> to turn it into the
/// <see cref="TimeSpan"/> the entity and the <c>time(7)</c> column want. Binding
/// straight to <see cref="TimeSpan"/> is what this replaces: the model binder
/// accepts <c>"1.06:00:00"</c> and <c>"-04:00:00"</c>, reports a bad value as an
/// untyped binding error rather than a named field, and puts a format the
/// clients never agreed to into the OpenAPI document.
/// </para>
/// </summary>
public static class TimeOfDay
{
    /// <summary>The accepted shape, as a caller reads it. Used in every message so they all say the same thing.</summary>
    public const string Pattern = "hh:mm:ss";

    /// <summary>A well-formed value, for documentation and error messages.</summary>
    public const string Example = "09:00:00";

    /// <summary>
    /// The custom <see cref="TimeSpan"/> format behind <see cref="Pattern"/>.
    /// The colons are escaped because <c>:</c> is a format specifier rather than
    /// a literal in a <see cref="TimeSpan"/> format string.
    /// </summary>
    private const string ParseFormat = @"hh\:mm\:ss";

    /// <summary>
    /// Exclusive upper bound. These values land in SQL Server <c>time(7)</c>
    /// columns, which hold a time of day and so cannot reach 24 hours.
    /// </summary>
    private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);

    /// <summary>
    /// True when <paramref name="value"/> is exactly <c>hh:mm:ss</c> and inside a
    /// single day. Exact parsing is the point: it is what rejects <c>"9:00:00"</c>,
    /// <c>"09:00"</c>, <c>"1.09:00:00"</c> and <c>"-09:00:00"</c>, all of which the
    /// ordinary <see cref="TimeSpan.TryParse(string, out TimeSpan)"/> accepts.
    /// </summary>
    public static bool TryParse(string? value, out TimeSpan parsed)
    {
        var wellFormed = TimeSpan.TryParseExact(
            value,
            ParseFormat,
            CultureInfo.InvariantCulture,
            TimeSpanStyles.None,
            out parsed);

        // The range check is belt and braces rather than dead code: it is the
        // one guarantee callers depend on, and it should not rest on the exact
        // reading of what "hh" means to TimeSpan.TryParseExact.
        if (wellFormed && parsed >= TimeSpan.Zero && parsed < OneDay)
        {
            return true;
        }

        parsed = default;
        return false;
    }

    /// <summary>
    /// Reads a required time of day.
    /// </summary>
    /// <param name="value">The raw payload value.</param>
    /// <param name="field">
    /// The property name as the client sent it - pass <c>nameof(...)</c> on the
    /// request, never a hand-typed string, so the key in the 400 matches the
    /// field the client can actually find in its payload.
    /// </param>
    /// <exception cref="ValidationException">Missing, or not <c>hh:mm:ss</c>.</exception>
    public static TimeSpan Parse(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(field, $"{field} is required, as a time of day in {Pattern} format - for example \"{Example}\".");
        }

        return TryParse(value, out var parsed)
            ? parsed
            : throw new ValidationException(field, MalformedMessage(field));
    }

    /// <summary>
    /// Reads an optional time of day. Absent - null, empty or whitespace - is
    /// <see langword="null"/> and not an error; present but malformed still is,
    /// because silently discarding a value the caller sent is worse than
    /// refusing it.
    /// </summary>
    /// <exception cref="ValidationException">Present, but not <c>hh:mm:ss</c>.</exception>
    public static TimeSpan? ParseOptional(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return TryParse(value, out var parsed)
            ? parsed
            : throw new ValidationException(field, MalformedMessage(field));
    }

    /// <summary>
    /// One message for every malformed time, so no two endpoints describe the
    /// same rule differently. It spells out both halves of the rule - the shape
    /// and the range - because "must be hh:mm:ss" alone does not tell a caller
    /// why <c>"24:00:00"</c> was refused.
    /// </summary>
    private static string MalformedMessage(string field) =>
        $"{field} must be a time of day in {Pattern} format, between \"00:00:00\" and \"23:59:59\" - " +
        $"for example \"{Example}\".";
}
