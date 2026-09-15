using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ETimeSheet.Shared.Responses;

namespace ETimeSheet.Tests.Helpers;

/// <summary>
/// Deserialisation helpers that mirror the API's JSON settings, so tests read
/// responses exactly the way a real client would.
/// </summary>
public static class ApiResponseExtensions
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    /// <summary>Reads the standard envelope and asserts nothing about the status code.</summary>
    public static async Task<ApiResponse<T>> ReadEnvelopeAsync<T>(this HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(Options);

        return envelope
               ?? throw new InvalidOperationException(
                   "The response body was empty or was not an ApiResponse envelope.");
    }

    /// <summary>Reads the envelope and returns its payload, failing if the payload is absent.</summary>
    public static async Task<T> ReadDataAsync<T>(this HttpResponseMessage response)
    {
        var envelope = await response.ReadEnvelopeAsync<T>();

        return envelope.Data
               ?? throw new InvalidOperationException(
                   $"The response envelope carried no data. Message: '{envelope.Message}'.");
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());

        return options;
    }
}
