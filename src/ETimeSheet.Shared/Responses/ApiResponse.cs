namespace ETimeSheet.Shared.Responses;

/// <summary>
/// The single response envelope used by every endpoint, success or failure, so
/// that clients only ever have to parse one shape.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public T? Data { get; init; }

    public IReadOnlyCollection<string> Errors { get; init; } = Array.Empty<string>();

    public static ApiResponse<T> Ok(T? data, string message = "") =>
        new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> Fail(string message, IReadOnlyCollection<string>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors ?? Array.Empty<string>() };
}

/// <summary>
/// Envelope for endpoints that carry no payload, and factory helpers for the
/// generic form so call sites do not have to repeat the type argument.
/// </summary>
public static class ApiResponse
{
    public static ApiResponse<object?> Ok(string message = "") =>
        ApiResponse<object?>.Ok(null, message);

    public static ApiResponse<T> Ok<T>(T? data, string message = "") =>
        ApiResponse<T>.Ok(data, message);

    public static ApiResponse<object?> Fail(string message, IReadOnlyCollection<string>? errors = null) =>
        ApiResponse<object?>.Fail(message, errors);
}
