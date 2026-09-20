using System.Text.Json;
using System.Text.Json.Serialization;
using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor;

/// <summary>One authored, independently playable unit and the root of its composition.</summary>
public sealed record MachineDocument(
    int Version,
    string Id,
    string DisplayName,
    string? CabinetAssetPath,
    MachineSurfaceAssignment[] SurfaceAssignments,
    MachineReelAssignment[] ReelAssignments,
    MachineEmulationRuntime Runtime,
    InputDefinitionModel[] InputDefinitions)
{
    public const int CurrentVersion = 1;

    public static MachineDocument Create(string displayName) => new(
        CurrentVersion,
        Guid.NewGuid().ToString("D"),
        displayName.Trim(),
        null,
        [],
        [],
        MachineEmulationRuntime.Empty,
        []);
}

public sealed record MachineSurfaceAssignment(string CabinetTargetId, string FaceAssetPath)
{
    public MachineSurfaceAssignment Normalized() => new(CabinetTargetId.Trim(), ProjectAssetPathService.NormalizeProjectRelativePath(FaceAssetPath.Trim()));
}

/// <summary>Phase-1 bridge only: a logical reel selects an embedded specification on the selected Cabinet.</summary>
public sealed record MachineReelAssignment(MachineObjectReference MachineReelReference, string CabinetReelSpecificationId)
{
    public MachineReelAssignment Normalized() => new(MachineReelReference, CabinetReelSpecificationId.Trim());
}

public sealed record MachineEmulationRuntime(string Kind, FruitMachinePlatformType Platform, JsonElement PlatformSettings)
{
    public const string EmulationKind = "Emulation";
    public static MachineEmulationRuntime Empty => ForPlatform(FruitMachinePlatformType.None, new { });

    public static MachineEmulationRuntime ForPlatform<T>(FruitMachinePlatformType platform, T settings) =>
        new(EmulationKind, platform, JsonSerializer.SerializeToElement(settings, MachineDocumentStorage.JsonOptions));

    public T ReadSettings<T>() => PlatformSettings.Deserialize<T>(MachineDocumentStorage.JsonOptions)
        ?? throw new InvalidOperationException($"Machine runtime settings for {Platform} are invalid.");
}

public static class MachineDocumentStorage
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Write(MachineDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Validate(document);
        return JsonSerializer.Serialize(Normalize(document), JsonOptions);
    }

    public static bool TryRead(string json, out MachineDocument document)
    {
        document = null!;
        try
        {
            var parsed = JsonSerializer.Deserialize<MachineDocument>(json, JsonOptions);
            if (parsed is null) return false;
            Validate(parsed);
            document = Normalize(parsed);
            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
        {
            return false;
        }
    }

    private static MachineDocument Normalize(MachineDocument value) => value with
    {
        Id = value.Id.Trim(),
        DisplayName = value.DisplayName.Trim(),
        CabinetAssetPath = string.IsNullOrWhiteSpace(value.CabinetAssetPath) ? null : ProjectAssetPathService.NormalizeProjectRelativePath(value.CabinetAssetPath.Trim()),
        SurfaceAssignments = (value.SurfaceAssignments ?? []).Select(item => item.Normalized()).ToArray(),
        ReelAssignments = (value.ReelAssignments ?? []).Select(item => item.Normalized()).ToArray(),
        InputDefinitions = value.InputDefinitions ?? []
    };

    private static void Validate(MachineDocument value)
    {
        if (value.Version != MachineDocument.CurrentVersion) throw new InvalidOperationException($"Unsupported Machine schema version {value.Version}.");
        if (!Guid.TryParse(value.Id, out _)) throw new InvalidOperationException("Machine ID must be a stable GUID.");
        if (string.IsNullOrWhiteSpace(value.DisplayName)) throw new InvalidOperationException("Machine display name is required.");
        if (value.Runtime is null || value.Runtime.Kind != MachineEmulationRuntime.EmulationKind) throw new InvalidOperationException("Phase 1 supports only an Emulation Machine runtime.");
        if ((value.SurfaceAssignments ?? []).GroupBy(item => item.CabinetTargetId, StringComparer.Ordinal).Any(group => group.Count() != 1)) throw new InvalidOperationException("Machine surface target assignments must be unique.");
        if ((value.ReelAssignments ?? []).Any(item => item.MachineReelReference.Kind != MachineObjectKind.Reel || item.MachineReelReference.IsEmpty)) throw new InvalidOperationException("Machine reel assignments must reference logical reels.");
    }
}

public sealed class ActiveMachineContext
{
    public string? ActiveMachineManifestPath { get; private set; }
    public MachineDocument? ActiveMachine { get; private set; }

    public void Select(string manifestPath, MachineDocument machine)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentNullException.ThrowIfNull(machine);
        ActiveMachineManifestPath = Path.GetFullPath(manifestPath);
        ActiveMachine = machine;
    }

    public void Clear() { ActiveMachineManifestPath = null; ActiveMachine = null; }

    public bool TrySelectOnlyMachine(EditorProject project)
    {
        var root = Path.Combine(project.AssetsDirectory, "Machines");
        var manifests = Directory.Exists(root) ? Directory.EnumerateFiles(root, ProjectAssetPathService.MachineManifestFileName, SearchOption.AllDirectories).Take(2).ToArray() : [];
        if (manifests.Length != 1 || !MachineDocumentStorage.TryRead(File.ReadAllText(manifests[0]), out var machine)) return false;
        Select(manifests[0], machine);
        return true;
    }
}
