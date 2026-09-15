namespace ETimeSheet.Application.Models.Common;

/// <summary>
/// A freshly issued access token and the instant it stops being valid.
/// The token value itself must never be logged.
/// </summary>
public class AccessToken
{
    public AccessToken(string value, DateTime expiresAtUtc)
    {
        Value = value;
        ExpiresAtUtc = expiresAtUtc;
    }

    public string Value { get; }

    public DateTime ExpiresAtUtc { get; }
}
