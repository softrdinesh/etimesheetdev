namespace ETimeSheet.Application.Interfaces.Services;

/// <summary>
/// Reads the HTML email templates shipped with the application. Keeps the
/// file system out of the Application layer.
/// </summary>
public interface IEmailTemplateStore
{
    /// <summary>
    /// Returns the template <paramref name="templateName"/> - a file name such
    /// as <c>"TimeLogTemplate.html"</c> - with its <c>[Placeholder]</c>s intact.
    /// </summary>
    /// <exception cref="FileNotFoundException">No template has that name.</exception>
    Task<string> GetAsync(
        string templateName,
        CancellationToken cancellationToken = default);
}
