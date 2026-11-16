using System.ComponentModel.DataAnnotations;

namespace ETimeSheet.Shared.Configuration;

/// <summary>
/// The password every account created by <c>create-timesheet-user</c> starts
/// with, in plain text: the value sent to <c>spc_Signup_timesheet_user</c> as
/// <c>@Password</c>, and the same value in the welcome email that tells the
/// user what it is.
/// <para>
/// The API never hashes, encrypts or encodes it. The procedure stores
/// <c>HASHBYTES('SHA2_512', @Password)</c> in <c>dbo.Signup.Pwd</c>, so the
/// stored form is derived from this one value and the two cannot drift apart -
/// which is exactly why the previously configured, pre-hashed
/// <c>Encrypted</c> value is gone. Do not bring it back.
/// </para>
/// <para>
/// <b>This is a credential in configuration.</b> One password is shared by
/// every account the API creates, it is mailed in the clear, and
/// <c>appsettings.json</c> is committed. Keep the real value in
/// <c>dotnet user-secrets</c> locally and in the deployment's environment
/// (<c>DefaultPassword__Plain</c>), and never log it.
/// </para>
/// </summary>
public class DefaultPasswordSettings
{
    public const string SectionName = "DefaultPassword";

    /// <summary>
    /// The default password as the user types it - sent to
    /// <c>spc_Signup_timesheet_user</c> as <c>@Password</c> for it to hash,
    /// and shown by the welcome email's <c>{{DefaultPassword}}</c>.
    /// Never logged, and never read back out of the database.
    /// </summary>
    [Required]
    public string Plain { get; set; } = string.Empty;
}
