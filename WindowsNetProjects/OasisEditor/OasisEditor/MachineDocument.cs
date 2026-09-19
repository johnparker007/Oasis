using System.Text.Json;
using System.Text.Json.Serialization;

namespace OasisEditor;

/// <summary>One authored, independently playable unit and the root of its composition.</summary>
public sealed record MachineDocument(
    int SchemaVersion,
    string Id,
    string DisplayName,
    string? CabinetAssetPath,
    MachineSurfaceAssignment[] SurfaceAssignments,
    MachineReelAssignment[] ReelAssignments,
    MachineEmulationRuntime Runtime,
    List<InputDefinitionModel> InputDefinitions)
{
    public const int CurrentSchemaVersion = 1;

    public static MachineDocument Create(string displayName) => new(
        CurrentSchemaVersion,
        Guid.NewGuid().ToString("D"),
        displayName.Trim(),
        null,
        [],
        [],
        MachineEmulationRuntime.Create(FruitMachinePlatformType.None),
        []);
}

public sealed record MachineSurfaceAssignment(string TargetId, string FaceAssetPath)
{
    public MachineSurfaceAssignment Normalized() => new(TargetId.Trim(), ProjectAssetPathService.NormalizeProjectRelativePath(FaceAssetPath.Trim()));
}

/// <summary>Phase-1 bridge only: resolves a logical reel to a selected Cabinet's embedded specification.</summary>
public sealed record MachineReelAssignment(MachineObjectReference MachineReelReference, string CabinetReelSpecificationId)
{
    public MachineReelAssignment Normalized() => new(MachineReelReference, CabinetReelSpecificationId.Trim());
}

/// <summary>
/// Narrow Phase-1 runtime definition. Only Settings for Platform is serialized and authoritative;
/// Player validates and retains this definition but does not host Fabric yet.
/// </summary>
public sealed record MachineEmulationRuntime(FruitMachinePlatformType Platform, object Settings)
{
    public const string Kind = "Emulation";

    public static MachineEmulationRuntime Create(FruitMachinePlatformType platform) => new(platform, platform switch
    {
        FruitMachinePlatformType.MPU5 => new Mpu5NativeRomSettings(),
        FruitMachinePlatformType.Epoch => new EpochNativeRomSettings(),
        FruitMachinePlatformType.MPU3 => new Mpu3ProjectSettings(),
        FruitMachinePlatformType.MaygayM1 => new M1ProjectSettings(),
        FruitMachinePlatformType.Scorpion4 => new Scorpion4ProjectSettings(),
        _ => new System6NativeRomSettings()
    });

    public T SettingsAs<T>() where T : class => Settings as T
        ?? throw new InvalidOperationException($"Machine runtime platform '{Platform}' does not use {typeof(T).Name} settings.");
}

public static class MachineDocumentStorage
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(MachineDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Validate(document);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", MachineDocument.CurrentSchemaVersion);
            writer.WriteString("id", document.Id);
            writer.WriteString("displayName", document.DisplayName);
            if (!string.IsNullOrWhiteSpace(document.CabinetAssetPath)) writer.WriteString("cabinetAssetPath", ProjectAssetPathService.NormalizeProjectRelativePath(document.CabinetAssetPath));
            writer.WritePropertyName("surfaceAssignments"); JsonSerializer.Serialize(writer, document.SurfaceAssignments.Select(x => x.Normalized()), Options);
            writer.WritePropertyName("reelAssignments"); JsonSerializer.Serialize(writer, document.ReelAssignments.Select(x => x.Normalized()), Options);
            writer.WritePropertyName("runtime");
            writer.WriteStartObject();
            writer.WriteString("kind", MachineEmulationRuntime.Kind);
            writer.WriteString("platform", document.Runtime.Platform.ToString());
            writer.WritePropertyName("platformSettings"); JsonSerializer.Serialize(writer, document.Runtime.Settings, document.Runtime.Settings.GetType(), Options);
            writer.WriteEndObject();
            writer.WritePropertyName("inputDefinitions"); JsonSerializer.Serialize(writer, document.InputDefinitions, Options);
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static bool TryRead(string? json, out MachineDocument document, out string error)
    {
        document = MachineDocument.Create("Machine"); error = string.Empty;
        if (string.IsNullOrWhiteSpace(json)) { error = "Machine document is empty."; return false; }
        try
        {
            using var parsed = JsonDocument.Parse(json);
            var root = parsed.RootElement;
            if (!root.TryGetProperty("schemaVersion", out var version) || version.GetInt32() != MachineDocument.CurrentSchemaVersion)
            { error = $"Unsupported Machine schema version. This editor supports only version {MachineDocument.CurrentSchemaVersion}."; return false; }
            var runtime = root.GetProperty("runtime");
            if (runtime.GetProperty("kind").GetString() != MachineEmulationRuntime.Kind) { error = "Machine runtime kind must be Emulation in Phase 1."; return false; }
            if (!Enum.TryParse<FruitMachinePlatformType>(runtime.GetProperty("platform").GetString(), out var platform)) { error = "Machine runtime platform is invalid."; return false; }
            var settingsElement = runtime.GetProperty("platformSettings");
            object settings = platform switch
            {
                FruitMachinePlatformType.MPU5 => settingsElement.Deserialize<Mpu5NativeRomSettings>(Options)!,
                FruitMachinePlatformType.Epoch => settingsElement.Deserialize<EpochNativeRomSettings>(Options)!,
                FruitMachinePlatformType.MPU3 => settingsElement.Deserialize<Mpu3ProjectSettings>(Options)!,
                FruitMachinePlatformType.MaygayM1 => settingsElement.Deserialize<M1ProjectSettings>(Options)!,
                FruitMachinePlatformType.Scorpion4 => settingsElement.Deserialize<Scorpion4ProjectSettings>(Options)!,
                _ => settingsElement.Deserialize<System6NativeRomSettings>(Options)!
            };
            document = new MachineDocument(
                version.GetInt32(), root.GetProperty("id").GetString() ?? string.Empty,
                root.GetProperty("displayName").GetString() ?? string.Empty,
                root.TryGetProperty("cabinetAssetPath", out var cabinet) ? cabinet.GetString() : null,
                root.TryGetProperty("surfaceAssignments", out var surfaces) ? surfaces.Deserialize<MachineSurfaceAssignment[]>(Options) ?? [] : [],
                root.TryGetProperty("reelAssignments", out var reels) ? reels.Deserialize<MachineReelAssignment[]>(Options) ?? [] : [],
                new MachineEmulationRuntime(platform, settings),
                root.TryGetProperty("inputDefinitions", out var inputs) ? inputs.Deserialize<List<InputDefinitionModel>>(Options) ?? [] : []);
            Validate(document);
            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
        { error = $"Invalid Machine document: {exception.Message}"; return false; }
    }

    private static void Validate(MachineDocument document)
    {
        if (document.SchemaVersion != MachineDocument.CurrentSchemaVersion) throw new InvalidOperationException("Only the current Machine schema can be written.");
        if (!Guid.TryParse(document.Id, out _)) throw new InvalidOperationException("Machine ID must be a stable GUID.");
        if (string.IsNullOrWhiteSpace(document.DisplayName)) throw new InvalidOperationException("Machine display name is required.");
        if (document.SurfaceAssignments.GroupBy(x => x.TargetId, StringComparer.Ordinal).Any(x => x.Count() > 1)) throw new InvalidOperationException("Machine surface target assignments must be unique.");
    }
}

/// <summary>Explicit workspace selection. Multiple Machines are never inferred by scanning composition assets.</summary>
public sealed class ActiveMachineContext
{
    public string? ActiveMachineManifestPath { get; private set; }
    public MachineDocument? ActiveMachine { get; private set; }
    public event EventHandler? Changed;

    public void Select(string manifestPath, MachineDocument machine)
    {
        ActiveMachineManifestPath = Path.GetFullPath(manifestPath);
        ActiveMachine = machine;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear() { ActiveMachineManifestPath = null; ActiveMachine = null; Changed?.Invoke(this, EventArgs.Empty); }
}
