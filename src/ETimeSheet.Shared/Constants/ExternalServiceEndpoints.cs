namespace ETimeSheet.Shared.Constants;

/// <summary>
/// Paths on the external email/document service, relative to
/// <see cref="Configuration.EmailServiceSettings.BaseUrl"/>.
/// <para>
/// The host comes from configuration, so it can differ per environment; the
/// paths are part of the service's contract and live here.
/// </para>
/// </summary>
public static class ExternalServiceEndpoints
{
    public static class Upload
    {
        /// <summary>
        /// Uploads one document for a project: <c>multipart/form-data</c> with a
        /// single part named <c>file</c>. <c>{projectcode}</c> names the project
        /// the document belongs to - see <see cref="DocumentProjectCodes"/>.
        /// </summary>
        public const string UpdateDocument = "/api/Upload/Updatedoc/{projectcode}";

        /// <summary><see cref="UpdateDocument"/> with the project code filled in, URL-escaped.</summary>
        public static string UpdateDocumentFor(string projectCode) =>
            UpdateDocument.Replace("{projectcode}", Uri.EscapeDataString(projectCode), StringComparison.Ordinal);
    }
}

/// <summary>
/// The project codes the document service files uploads under.
/// </summary>
public static class DocumentProjectCodes
{
    /// <summary>Timesheet client setup - a client's logo.</summary>
    public const string ClientSetup = "TSCLT";
}
