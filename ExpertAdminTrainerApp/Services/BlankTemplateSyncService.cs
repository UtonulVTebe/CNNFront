using System.IO;
using ExpertAdminTrainerApp.Domain;

namespace ExpertAdminTrainerApp.Services;

/// <summary>
/// Синхронизация JSON шаблона разметки бланков с API: файл через <c>api/Files</c> (category <c>blanks</c>),
/// ссылка — материал КИМа с <see cref="MaterialKind.Blanks"/> (как в Swagger).
/// </summary>
public sealed class BlankTemplateSyncService(IApiClient apiClient)
{
    /// <summary>Заголовок материала на сервере, по которому находим JSON шаблона среди прочих «бланков».</summary>
    public const string RemoteMaterialTitle = "Разметка бланков (JSON)";

    /// <summary>Категория для <see cref="IApiClient.UploadFileAsync"/> (как у ручной загрузки в админке).</summary>
    public const string UploadFileCategory = "blanks";

    public async Task PushAsync(BlankTemplateDefinition template, CancellationToken ct = default)
    {
        var json = BlankTemplateService.SerializeTemplate(template);
        var tempPath = Path.Combine(Path.GetTempPath(), $"blank_template_{template.CnnId}_{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(tempPath, json, ct);
        try
        {
            var upload = await apiClient.UploadFileAsync(tempPath, UploadFileCategory, ct);
            var url = upload.Url?.Trim();
            if (string.IsNullOrEmpty(url))
                throw new InvalidOperationException("Сервер не вернул URL загруженного файла.");

            var details = await apiClient.GetCnnDetailsAsync(template.CnnId, ct);
            var existing = FindTemplateMaterial(details.Materials);

            var write = new CnnMaterialWriteDto
            {
                Kind = MaterialKind.Blanks,
                Title = RemoteMaterialTitle,
                Url = url,
                SortOrder = existing?.SortOrder ?? 0
            };

            if (existing is not null)
                await apiClient.UpdateMaterialAsync(template.CnnId, existing.Id, write, ct);
            else
                await apiClient.CreateMaterialAsync(template.CnnId, write, ct);
        }
        finally
        {
            try { File.Delete(tempPath); } catch { /* ignore */ }
        }
    }

    /// <summary>Загружает шаблон с сервера или <c>null</c>, если материала с нужным заголовком нет.</summary>
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

    private static CnnMaterialDto? FindTemplateMaterial(IReadOnlyList<CnnMaterialDto> materials) =>
        materials
            .Where(m => m.Kind == MaterialKind.Blanks
                        && string.Equals(m.Title?.Trim(), RemoteMaterialTitle, StringComparison.Ordinal))
            .OrderByDescending(m => m.Id)
            .FirstOrDefault();
}
