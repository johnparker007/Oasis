using System.Text.Json;
using System.Text.Json.Serialization;

namespace OasisEditor.Features.CabinetEditor.Models;

public static class CabinetDocumentStorage
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static string Serialize(CabinetDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonSerializer.Serialize(document with { Version = 9 }, Options);
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
            if (parsed?.Model is null || string.IsNullOrWhiteSpace(parsed.Model.Path) || parsed.Version != 9)
            {
                return false;
            }

            document = parsed with
            {
                SurfaceTargetSettings = (parsed.SurfaceTargetSettings ?? [])
                    .Where(targetSettings => !string.IsNullOrWhiteSpace(targetSettings.TargetId))
                    .Select(targetSettings => targetSettings.Normalized())
                    .ToArray(),
                Reflections = (parsed.Reflections ?? []).Where(reflection => !string.IsNullOrWhiteSpace(reflection.Id) && reflection.Sources is not null && reflection.Settings is not null).Select(reflection => reflection.Normalized()).ToArray()
            };
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
