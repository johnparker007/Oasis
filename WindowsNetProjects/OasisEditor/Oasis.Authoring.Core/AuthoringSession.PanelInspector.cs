namespace OasisEditor;

public sealed record PanelElementInspectorSettings(
    string? AssetPath = null,
    string? SecondaryAssetPath = null,
    string? BackgroundColorHex = null,
    int? LampNumber = null,
    string? TextColorHex = null,
    string? DisplayText = null);

public sealed partial class AuthoringSession
{
    public PanelElementInspectorSettings GetPanelElementInspectorSettings(string id)
    {
        var element = RequireDocument().Elements.FirstOrDefault(value => value.ObjectId == id)
            ?? throw new KeyNotFoundException($"Panel element '{id}' was not found.");
        return new(element.AssetPath, element.SecondaryAssetPath,
            element.Kind == "background" ? element.OnColorHex : null,
            element.Kind == "label" ? element.LampNumber : null,
            element.Kind == "label" ? element.TextColorHex : null,
            element.Kind == "label" ? element.DisplayText : null);
    }

    public bool UpdatePanelElementInspectorSettings(string id, PanelElementInspectorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var document = RequireDocument();
        var index = Array.FindIndex(document.Elements, value => value.ObjectId == id);
        if (index < 0) throw new KeyNotFoundException($"Panel element '{id}' was not found.");
        var current = document.Elements[index];
        var kind = current.Kind;
        if (settings.LampNumber is < 0)
            throw new ArgumentOutOfRangeException(nameof(settings), "Lamp number must be zero or greater.");
        var backgroundColor = NormalizeOptional(settings.BackgroundColorHex);
        var textColor = NormalizeOptional(settings.TextColorHex);
        if (backgroundColor is not null && !IsValidColor(backgroundColor))
            throw new ArgumentException("Background color must use #RRGGBB or #AARRGGBB notation.", nameof(settings));
        if (textColor is not null && !IsValidColor(textColor))
            throw new ArgumentException("Text color must use #RRGGBB or #AARRGGBB notation.", nameof(settings));

        var assetPath = NormalizeAssetPath(settings.AssetPath, current.AssetPath, kind is "image" or "background" or "lamp" or "reel");
        var secondaryPath = NormalizeAssetPath(settings.SecondaryAssetPath, current.SecondaryAssetPath, kind is "background" or "reel");
        var next = current with
        {
            AssetPath = assetPath,
            SecondaryAssetPath = secondaryPath,
            OnColorHex = kind == "background" ? backgroundColor : current.OnColorHex,
            LampNumber = kind == "label" ? settings.LampNumber : current.LampNumber,
            TextColorHex = kind == "label" ? textColor : current.TextColorHex,
            DisplayText = kind == "label" ? NormalizeOptional(settings.DisplayText) : current.DisplayText
        };
        if (next == current) return false;
        var elements = (PanelElementFile[])document.Elements.Clone();
        elements[index] = next;
        Commit(document with { Elements = elements });
        return true;
    }

    private string? NormalizeAssetPath(string? value, string? prior, bool supported)
    {
        if (!supported) return prior;
        var normalized = NormalizeOptional(value);
        if (normalized is null) return null;
        if (Path.IsPathRooted(normalized))
            throw new ArgumentException("Asset paths must be relative to the open project.", nameof(value));
        var root = RequireProject();
        var resolved = WorkspacePathPolicy.ResolveConfined(root, normalized, requireExists: false);
        WorkspacePathPolicy.RejectReparsePoints(root, Path.GetRelativePath(root, resolved), requireExists: false);
        return ProjectAssetPathService.NormalizeProjectRelativePath(Path.GetRelativePath(root, resolved));
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsValidColor(string value)
        => value.Length is 7 or 9 && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);
}
