using System.Net;

namespace ETimeSheet.Shared.Exceptions;

/// <summary>
/// Request payload failed validation. Maps to HTTP 400.
/// </summary>
public class ValidationException : AppException
{
    public ValidationException(IReadOnlyDictionary<string, string[]> failures)
        : base(HttpStatusCode.BadRequest, "One or more validation errors occurred.", Flatten(failures))
    {
        Failures = failures;
    }

    public ValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = new[] { message } })
    {
    }

    /// <summary>Validation failures keyed by property name.</summary>
    public IReadOnlyDictionary<string, string[]> Failures { get; }

    private static string[] Flatten(IReadOnlyDictionary<string, string[]> failures) =>
        failures
            .SelectMany(failure => failure.Value.Select(message => $"{failure.Key}: {message}"))
            .ToArray();
}
