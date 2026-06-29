using System.Text.Json;
using System.Text.Json.Serialization;

namespace OasisEditor.Features.CabinetEditor.Models;

public static class CabinetDocumentStorage
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(CabinetDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonSerializer.Serialize(document, Options);
    }

    public static bool TryRead(string? json, out CabinetDocument document)
    {
        document = CabinetDocument.Empty;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<CabinetDocument>(json, Options);
            if (parsed?.Model is null || string.IsNullOrWhiteSpace(parsed.Model.Path))
            {
                return false;
            }

            document = parsed with
            {
                TargetOverrides = (parsed.TargetOverrides ?? [])
                    .Where(targetOverride => !string.IsNullOrWhiteSpace(targetOverride.TargetId))
                    .Select(targetOverride => targetOverride.Normalized())
                    .ToArray(),
                Preview = (parsed.Preview ?? CabinetPreviewSettings.Default).Normalized()
            };
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
