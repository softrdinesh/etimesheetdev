namespace ETimeSheet.Shared.Enums;

/// <summary>
/// Application roles. The numeric values are persisted in the JWT
/// <see cref="Constants.ClaimConstants.RoleId"/> claim, so they must stay stable.
/// </summary>
public enum RoleType
{
    Employee = 1,
    Manager = 2,
    Administrator = 3
}
