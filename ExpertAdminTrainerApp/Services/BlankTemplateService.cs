using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExpertAdminTrainerApp.Domain;

namespace ExpertAdminTrainerApp.Services;

public class BlankTemplateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _templatesDir;

    public BlankTemplateService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _templatesDir = Path.Combine(appData, "ExpertAdminTrainerApp", "templates");
        Directory.CreateDirectory(_templatesDir);
    }

    public string TemplatesDirectory => _templatesDir;

    public async Task SaveTemplateAsync(BlankTemplateDefinition template)
    {
        var path = GetTemplatePath(template.CnnId);
        var json = JsonSerializer.Serialize(template, JsonOptions);
        await File.WriteAllTextAsync(path, json);
    }

    public async Task<BlankTemplateDefinition?> LoadTemplateAsync(int cnnId)
    {
        var path = GetTemplatePath(cnnId);
        if (!File.Exists(path))
            return null;

        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<BlankTemplateDefinition>(json, JsonOptions);
    }

    public async Task ExportTemplateAsync(int cnnId, string destinationPath)
    {
        var sourcePath = GetTemplatePath(cnnId);
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"Template for CNN {cnnId} not found.");

        var json = await File.ReadAllTextAsync(sourcePath);
        await File.WriteAllTextAsync(destinationPath, json);
    }

    public async Task<BlankTemplateDefinition?> ImportTemplateAsync(string sourcePath)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"File not found: {sourcePath}");

        var json = await File.ReadAllTextAsync(sourcePath);
        var template = JsonSerializer.Deserialize<BlankTemplateDefinition>(json, JsonOptions);

        if (template is not null)
            await SaveTemplateAsync(template);

        return template;
    }

    public bool TemplateExists(int cnnId) => File.Exists(GetTemplatePath(cnnId));

    private string GetTemplatePath(int cnnId) => Path.Combine(_templatesDir, $"{cnnId}.json");
}
