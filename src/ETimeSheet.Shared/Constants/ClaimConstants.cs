namespace ETimeSheet.Shared.Constants;

/// <summary>
/// The single source of truth for custom JWT claim types.
/// Claim names must never be written as inline string literals anywhere else.
/// </summary>
public static class ClaimConstants
{
    /// <summary>Identifier of the authenticated user. Value is an <see cref="int"/>.</summary>
    public const string UserId = "uid";

    /// <summary>Identifier of the authenticated user's role. Value is an <see cref="int"/>.</summary>
    public const string RoleId = "rid";
}
