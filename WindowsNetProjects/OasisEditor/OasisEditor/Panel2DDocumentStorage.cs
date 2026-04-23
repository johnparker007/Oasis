using System.Text.Json;

namespace OasisEditor;

internal static class Panel2DDocumentStorage
{
    public static string Serialize(string title, string summary)
    {
        var payload = new Panel2DDocumentFile
        {
            SchemaVersion = 1,
            Title = title,
            Summary = summary,
            SavedAtUtc = DateTime.UtcNow
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    public static bool TryCreateSummary(string content, out string summary)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<Panel2DDocumentFile>(content);
            if (parsed is null)
            {
                summary = "Panel document opened (file is empty).";
                return true;
            }

            summary = string.IsNullOrWhiteSpace(parsed.Summary)
                ? "Panel document opened."
                : parsed.Summary.Trim();
            return true;
        }
        catch (JsonException)
        {
            summary = string.Empty;
            return false;
        }
    }
}

internal sealed class Panel2DDocumentFile
{
    public int SchemaVersion { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public DateTime SavedAtUtc { get; init; }
}
