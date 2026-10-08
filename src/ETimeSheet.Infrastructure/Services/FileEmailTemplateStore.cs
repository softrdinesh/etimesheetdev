using System.Collections.Concurrent;
using ETimeSheet.Application.Interfaces.Services;

namespace ETimeSheet.Infrastructure.Services;

/// <summary>
/// <see cref="IEmailTemplateStore"/> over the HTML files in
/// <c>Assets/EmailTemplates/</c>, which the build copies next to the binaries.
/// Each file is read once and kept: a template changes only with a deployment.
/// </summary>
public class FileEmailTemplateStore : IEmailTemplateStore
{
    private static readonly string TemplateDirectory =
        Path.Combine(AppContext.BaseDirectory, "Assets", "EmailTemplates");

    private readonly ConcurrentDictionary<string, string> _templates = new(StringComparer.OrdinalIgnoreCase);

    public async Task<string> GetAsync(
        string templateName,
        CancellationToken cancellationToken = default)
    {
        if (_templates.TryGetValue(templateName, out var cached))
        {
            return cached;
        }

        // GetFileName, so a name can never climb out of the template folder.
        var path = Path.Combine(TemplateDirectory, Path.GetFileName(templateName));

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Email template '{templateName}' was not found.", path);
        }

        var template = await File.ReadAllTextAsync(path, cancellationToken);

        return _templates.GetOrAdd(templateName, template);
    }
}
