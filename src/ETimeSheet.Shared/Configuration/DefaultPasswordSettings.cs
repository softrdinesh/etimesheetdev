using System.ComponentModel.DataAnnotations;

namespace ETimeSheet.Shared.Configuration;

/// <summary>
/// The password every account created by <c>create-timesheet-user</c> starts
/// with: the value written to <c>dbo.Signup.Pwd</c>, and the same password in
/// plain text for the welcome email that tells the user what it is.
/// <para>
/// Both describe <b>one</b> password. <see cref="Encrypted"/> is what the
/// login checks; <see cref="Plain"/> is what the user types. If the two ever
/// stop being the same password, every new account is created unable to sign
/// in - nothing in this API can detect that, because it never encrypts or
/// verifies anything itself.
/// </para>
/// <para>
/// <b>These are credentials in configuration.</b> One password is shared by
/// every account the API creates, it is mailed in the clear, and
/// <c>appsettings.json</c> is committed. Keep the real values in
/// <c>dotnet user-secrets</c> locally and in the deployment's environment
/// (<c>DefaultPassword__Encrypted</c> / <c>DefaultPassword__Plain</c>), and
/// never log either of them.
/// </para>
/// </summary>
public class DefaultPasswordSettings
{
    public const string SectionName = "DefaultPassword";

    /// <summary>
    /// The password as <c>dbo.Signup.Pwd</c> stores it, copied verbatim.
    /// <para>
    /// The column is <c>nvarchar</c>, and <c>spc_Signup_timesheet_user</c>
    /// fills it by letting SQL Server convert a <c>binary(64)</c>
    /// <c>HASHBYTES('SHA2_512', ...)</c> into it - so a stored value reads as
    /// mojibake. That is the value to put here, exactly as
    /// <c>SELECT Pwd FROM dbo.Signup</c> returns it; it is written back
    /// unchanged, with no hashing, encoding or trimming of any kind.
    /// </para>
    /// <para>
    /// Paste the characters as they are - <c>appsettings.json</c> is UTF-8 and
    /// the configuration reader returns them unchanged. <c>\uXXXX</c> escapes
    /// are equally valid, and worth using if an editor or a deployment
    /// pipeline is likely to re-encode the file: a character lost that way is
    /// a password nobody can sign in with, and nothing here would notice.
    /// </para>
    /// </summary>
    [Required]
    public string Encrypted { get; set; } = string.Empty;

    /// <summary>
    /// The same password in plain text - what the user types to sign in, and
    /// the only thing the welcome email's <c>{{DefaultPassword}}</c> shows.
    /// Never written to the database and never logged.
    /// </summary>
    [Required]
    public string Plain { get; set; } = string.Empty;
}
