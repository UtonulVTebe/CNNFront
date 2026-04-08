namespace ExpertAdminTrainerApp.Services;

public static class TemplateMediaResolver
{
    public static string Resolve(string imagePath, string? templateJsonMaterialUrl)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return string.Empty;

        var trimmed = imagePath.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var abs) &&
            (abs.Scheme == Uri.UriSchemeHttp || abs.Scheme == Uri.UriSchemeHttps || abs.Scheme == Uri.UriSchemeFile))
            return trimmed;

        if (string.IsNullOrWhiteSpace(templateJsonMaterialUrl)
            || !Uri.TryCreate(templateJsonMaterialUrl.Trim(), UriKind.Absolute, out var jsonUri))
            return trimmed;

        var path = jsonUri.AbsolutePath;
        var slash = path.LastIndexOf('/');
        var dir = slash >= 0 ? path[..(slash + 1)] : "/";
        return jsonUri.GetLeftPart(UriPartial.Authority) + dir + trimmed.TrimStart('/');
    }
}
