using TrainerStudApp.Domain;

namespace TrainerStudApp.Services;

/// <summary>
/// Загрузка JSON шаблона бланков с API: материал КИМа <see cref="MaterialKind.Blanks"/> с известным заголовком.
/// </summary>
public sealed class BlankTemplateSyncService(IApiClient apiClient)
{
    public const string RemoteMaterialTitle = "Разметка бланков (JSON)";

    public static CnnMaterialDto? FindTemplateMaterial(IReadOnlyList<CnnMaterialDto> materials) =>
        materials
            .Where(m => m.Kind == MaterialKind.Blanks
                        && string.Equals(m.Title?.Trim(), RemoteMaterialTitle, StringComparison.Ordinal))
            .OrderByDescending(m => m.Id)
            .FirstOrDefault();

    public async Task<BlankTemplateDefinition?> PullAsync(int cnnId, CancellationToken ct = default)
    {
        var details = await apiClient.GetCnnDetailsAsync(cnnId, ct);
        var material = FindTemplateMaterial(details.Materials);
        if (material is null || string.IsNullOrWhiteSpace(material.Url))
            return null;

        var body = await apiClient.DownloadTextAsync(material.Url.Trim(), ct);
        var template = BlankTemplateService.DeserializeTemplate(body);
        if (template is null)
            return null;

        if (template.CnnId != cnnId)
            template.CnnId = cnnId;

        return template;
    }

    /// <summary>URL JSON-материала разметки (для разрешения относительных путей к сканам страниц).</summary>
    public async Task<string?> GetTemplateJsonMaterialUrlAsync(int cnnId, CancellationToken ct = default)
    {
        var details = await apiClient.GetCnnDetailsAsync(cnnId, ct);
        return FindTemplateMaterial(details.Materials)?.Url?.Trim();
    }
}
