using System.Text.Json;

namespace OasisEditor;

internal static class Panel2DDocumentStorage
{
    public static string Serialize(string title, string summary, IReadOnlyList<PanelElementFile> elements)
    {
        var payload = new Panel2DDocumentFile
        {
            SchemaVersion = 1,
            Title = title,
            Summary = summary,
            SavedAtUtc = DateTime.UtcNow,
            Elements = elements.ToArray()
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    public static bool TryCreateSummary(string content, out string summary)
    {
        if (!TryRead(content, out var document))
        {
            summary = string.Empty;
            return false;
        }

        summary = string.IsNullOrWhiteSpace(document.Summary)
            ? "Panel document opened."
            : document.Summary.Trim();
        return true;
    }

    public static bool TryRead(string content, out Panel2DDocumentFile document)
    {
        try
        {
            document = JsonSerializer.Deserialize<Panel2DDocumentFile>(content) ?? new Panel2DDocumentFile();
            return true;
        }
        catch (JsonException)
        {
            document = new Panel2DDocumentFile();
            return false;
        }
    }

    public static string SerializeLayout(IReadOnlyList<PanelElementFile> elements)
    {
        return JsonSerializer.Serialize(elements);
    }

    public static IReadOnlyList<PanelElementFile> DeserializeLayout(string? layoutJson)
    {
        if (string.IsNullOrWhiteSpace(layoutJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<PanelElementFile>>(layoutJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

internal sealed class Panel2DDocumentFile
{
    public int SchemaVersion { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public DateTime SavedAtUtc { get; init; }
    public PanelElementFile[] Elements { get; init; } = [];
}

internal sealed class PanelElementFile
{
    public string Name { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
}
