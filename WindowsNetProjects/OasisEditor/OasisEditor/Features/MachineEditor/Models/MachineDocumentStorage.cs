using System.Text.Json;
using System.Text.Json.Serialization;

namespace OasisEditor.Features.MachineEditor.Models;

public static class MachineDocumentStorage
{
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(MachineDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonSerializer.Serialize(document with { Version = CurrentSchemaVersion }, Options);
    }

    public static bool TryRead(string? json, out MachineDocument document)
    {
        document = MachineDocumentExtensions.Empty();
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<MachineDocument>(json, Options);
            if (parsed is null || parsed.Version != CurrentSchemaVersion || string.IsNullOrWhiteSpace(parsed.Id))
            {
                return false;
            }

            document = parsed with
            {
                Id = parsed.Id.Trim(),
                Title = string.IsNullOrWhiteSpace(parsed.Title) ? "Machine" : parsed.Title.Trim(),
                Summary = string.IsNullOrWhiteSpace(parsed.Summary) ? null : parsed.Summary.Trim(),
                CabinetAssetPath = string.IsNullOrWhiteSpace(parsed.CabinetAssetPath)
                    ? null
                    : parsed.CabinetAssetPath.Trim().Replace('\\', '/'),
                SurfaceAssignments = (parsed.SurfaceAssignments ?? [])
                    .Where(value => !string.IsNullOrWhiteSpace(value.TargetId) && !string.IsNullOrWhiteSpace(value.FaceAssetPath))
                    .Select(value => value.Normalized())
                    .ToArray(),
                ReelAssignments = (parsed.ReelAssignments ?? [])
                    .Where(value => value.MachineReelReference.Kind == MachineObjectKind.Reel
                        && !value.MachineReelReference.IsEmpty
                        && !string.IsNullOrWhiteSpace(value.ReelSpecificationId))
                    .Select(value => value.Normalized())
                    .ToArray(),
                Runtime = parsed.Runtime ?? MachineRuntimeDefinition.CreateDefault(),
                InputDefinitions = parsed.InputDefinitions ?? []
            };
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
