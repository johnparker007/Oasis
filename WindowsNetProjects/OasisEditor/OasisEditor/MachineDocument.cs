using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OasisEditor;

/// <summary>One authored, independently playable unit and the root of its composition.</summary>
public sealed record MachineDocument(
    int SchemaVersion,
    string Id,
    string DisplayName,
    AssetReference? CabinetAsset,
    MachineSurfaceAssignment[] SurfaceAssignments,
    MachineReelAssignment[] ReelAssignments,
    MachineObject3DInstance[] ObjectInstances,
    RuntimeDefinition Runtime,
    List<InputDefinitionModel> InputDefinitions,
    MachineAnchor[]? Anchors = null)
{
    public const int CurrentSchemaVersion = 6;

    [JsonIgnore]
    public EmulationRuntimeDefinition EmulationRuntime => Runtime as EmulationRuntimeDefinition
        ?? throw new InvalidOperationException($"Machine '{DisplayName}' Runtime '{Runtime.Kind}' is not Emulation.");

    public static MachineDocument Create(string displayName) => new(
        CurrentSchemaVersion,
        Guid.NewGuid().ToString("D"),
        displayName.Trim(),
        null,
        [],
        [],
        [],
        EmulationRuntimeDefinition.Create(FruitMachinePlatformType.None),
        [],
        []);
}

/// <summary>A three-dimensional value in Machine authoring coordinates.</summary>
public sealed record MachineVector3(double X, double Y, double Z)
{
    public static MachineVector3 Zero { get; } = new(0, 0, 0);
    public static MachineVector3 One { get; } = new(1, 1, 1);
}

/// <summary>
/// A Machine-level transform. Rotation is authored as Euler angles in degrees around X, Y and Z;
/// runtime conversion applies them in Unity's standard Z-X-Y order. There is no parent transform in schema 6.
/// </summary>
public sealed record MachineObjectTransform(MachineVector3 Position, MachineVector3 Rotation, MachineVector3 Scale)
{
    public static MachineObjectTransform Identity { get; } = new(MachineVector3.Zero, MachineVector3.Zero, MachineVector3.One);
}

/// <summary>One independently identified use of a reusable Object3D asset in a Machine.</summary>
public sealed record MachineObject3DInstance(string Id, string DisplayName, AssetReference? ObjectAsset, MachineObjectTransform Transform);

/// <summary>A stable, Machine-owned position and XYZ Euler rotation in composition space. Anchors have no scale or scene object.</summary>
public sealed record MachineAnchor(string Id, string DisplayName, MachineVector3 Position, MachineVector3 Rotation)
{
    public static MachineAnchor Create(string id, string displayName = "Anchor") => new(id, displayName, MachineVector3.Zero, MachineVector3.Zero);
}

public sealed record MachineSurfaceAssignment(string TargetId, string FaceAssetPath)
{
    public MachineSurfaceAssignment Normalized() => new(TargetId.Trim(), ProjectAssetPathService.NormalizeProjectRelativePath(FaceAssetPath.Trim()));
}

/// <summary>Resolves a logical Face reel to a reusable physical Reel asset.</summary>
public sealed record MachineReelAssignment(MachineObjectReference MachineReelReference, AssetReference ReelAsset)
{
    public MachineReelAssignment Normalized() => new(MachineReelReference, new AssetReference(ReelAsset.Scope, ReelAsset.Path));
}

/// <summary>Authored configuration describing how a Machine behaves.</summary>
public abstract record RuntimeDefinition
{
    public abstract string Kind { get; }
}

/// <summary>The explicit subset of platform identifiers currently implemented by Oasis Emulation.</summary>
public static class EmulationRuntimePlatforms
{
    public static IReadOnlyList<FruitMachinePlatformType> Supported { get; } =
    [
        FruitMachinePlatformType.None,
        FruitMachinePlatformType.Impact,
        FruitMachinePlatformType.MPU5,
        FruitMachinePlatformType.Epoch,
        FruitMachinePlatformType.MPU3,
        FruitMachinePlatformType.MaygayM1,
        FruitMachinePlatformType.Scorpion4
    ];

    public static bool IsSupported(FruitMachinePlatformType platform) => Supported.Contains(platform);
}

/// <summary>Authored fruit-machine emulation configuration. Fabric/Amber remain execution backends.</summary>
public sealed record EmulationRuntimeDefinition(FruitMachinePlatformType Platform, object PlatformSettings) : RuntimeDefinition
{
    public const string RuntimeKind = "Emulation";
    public override string Kind => RuntimeKind;

    public static EmulationRuntimeDefinition Create(FruitMachinePlatformType platform) => new(platform, platform switch
    {
        FruitMachinePlatformType.None => new System6NativeRomSettings(),
        FruitMachinePlatformType.Impact => new System6NativeRomSettings(),
        FruitMachinePlatformType.MPU5 => new Mpu5NativeRomSettings(),
        FruitMachinePlatformType.Epoch => new EpochNativeRomSettings(),
        FruitMachinePlatformType.MPU3 => new Mpu3ProjectSettings(),
        FruitMachinePlatformType.MaygayM1 => new M1ProjectSettings(),
        FruitMachinePlatformType.Scorpion4 => new Scorpion4ProjectSettings(),
        _ => throw new NotSupportedException($"Emulation platform '{platform}' is not currently supported.")
    });

    public T PlatformSettingsAs<T>() where T : class => PlatformSettings as T
        ?? throw new InvalidOperationException($"Machine runtime platform '{Platform}' does not use {typeof(T).Name} settings.");
}

public static class RuntimeDefinitionValidation
{
    public static void Validate(string machineName, RuntimeDefinition runtime)
    {
        if (runtime is not EmulationRuntimeDefinition emulation)
            throw new InvalidOperationException($"Machine '{machineName}' has unsupported Runtime '{runtime?.Kind ?? "(missing)"}'.");
        if (!EmulationRuntimePlatforms.IsSupported(emulation.Platform))
            throw new InvalidOperationException($"Machine '{machineName}', Runtime '{emulation.Kind}', Platform '{emulation.Platform}' is an unsupported Emulation platform.");
        var expected = EmulationRuntimeDefinition.Create(emulation.Platform).PlatformSettings.GetType();
        if (emulation.PlatformSettings is null || emulation.PlatformSettings.GetType() != expected)
            throw new InvalidOperationException($"Machine '{machineName}', Runtime '{emulation.Kind}', Platform '{emulation.Platform}' requires {expected.Name} settings.");
    }
}

public static class MachineDocumentStorage
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    static MachineDocumentStorage() => Options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));

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
            if (document.CabinetAsset is not null) { writer.WritePropertyName("cabinetAsset"); JsonSerializer.Serialize(writer, document.CabinetAsset, Options); }
            writer.WritePropertyName("surfaceAssignments"); JsonSerializer.Serialize(writer, document.SurfaceAssignments.Select(x => x.Normalized()), Options);
            writer.WritePropertyName("reelAssignments"); JsonSerializer.Serialize(writer, document.ReelAssignments.Select(x => x.Normalized()), Options);
            writer.WritePropertyName("objectInstances"); JsonSerializer.Serialize(writer, document.ObjectInstances, Options);
            writer.WritePropertyName("anchors"); JsonSerializer.Serialize(writer, document.Anchors, Options);
            writer.WritePropertyName("runtime");
            writer.WriteStartObject();
            switch (document.Runtime)
            {
                case EmulationRuntimeDefinition emulation:
                    writer.WriteString("kind", emulation.Kind);
                    writer.WriteString("platform", emulation.Platform.ToString());
                    writer.WritePropertyName("platformSettings");
                    JsonSerializer.Serialize(writer, emulation.PlatformSettings, emulation.PlatformSettings.GetType(), Options);
                    break;
                default:
                    throw new InvalidOperationException($"Machine '{document.DisplayName}' has unsupported Runtime '{document.Runtime.Kind}'.");
            }
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
            if (runtime.GetProperty("kind").GetString() != EmulationRuntimeDefinition.RuntimeKind) { error = "Machine runtime kind is unsupported. This editor supports only Emulation."; return false; }
            if (!Enum.TryParse<FruitMachinePlatformType>(runtime.GetProperty("platform").GetString(), out var platform)) { error = "Machine runtime platform is invalid."; return false; }
            if (!EmulationRuntimePlatforms.IsSupported(platform)) { error = $"Machine Runtime 'Emulation' Platform '{platform}' is an unsupported Emulation platform."; return false; }
            var settingsElement = runtime.GetProperty("platformSettings");
            object settings = platform switch
            {
                FruitMachinePlatformType.None => settingsElement.Deserialize<System6NativeRomSettings>(Options)!,
                FruitMachinePlatformType.Impact => settingsElement.Deserialize<System6NativeRomSettings>(Options)!,
                FruitMachinePlatformType.MPU5 => settingsElement.Deserialize<Mpu5NativeRomSettings>(Options)!,
                FruitMachinePlatformType.Epoch => settingsElement.Deserialize<EpochNativeRomSettings>(Options)!,
                FruitMachinePlatformType.MPU3 => settingsElement.Deserialize<Mpu3ProjectSettings>(Options)!,
                FruitMachinePlatformType.MaygayM1 => settingsElement.Deserialize<M1ProjectSettings>(Options)!,
                FruitMachinePlatformType.Scorpion4 => settingsElement.Deserialize<Scorpion4ProjectSettings>(Options)!,
                _ => throw new NotSupportedException($"Emulation platform '{platform}' is not currently supported.")
            };
            document = new MachineDocument(
                version.GetInt32(), root.GetProperty("id").GetString() ?? string.Empty,
                root.GetProperty("displayName").GetString() ?? string.Empty,
                root.TryGetProperty("cabinetAsset", out var cabinet) ? cabinet.Deserialize<AssetReference>(Options) : null,
                root.TryGetProperty("surfaceAssignments", out var surfaces) ? surfaces.Deserialize<MachineSurfaceAssignment[]>(Options) ?? [] : [],
                root.TryGetProperty("reelAssignments", out var reels) ? reels.Deserialize<MachineReelAssignment[]>(Options) ?? [] : [],
                root.GetProperty("objectInstances").Deserialize<MachineObject3DInstance[]>(Options) ?? throw new InvalidOperationException("Machine ObjectInstances collection is required."),
                new EmulationRuntimeDefinition(platform, settings),
                root.TryGetProperty("inputDefinitions", out var inputs) ? inputs.Deserialize<List<InputDefinitionModel>>(Options) ?? [] : [],
                root.GetProperty("anchors").Deserialize<MachineAnchor[]>(Options) ?? throw new InvalidOperationException("Machine Anchors collection is required."));
            Validate(document);
            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException or NotSupportedException or KeyNotFoundException)
        { error = $"Invalid Machine document: {exception.Message}"; return false; }
    }

    private static void Validate(MachineDocument document)
    {
        if (document.SchemaVersion != MachineDocument.CurrentSchemaVersion) throw new InvalidOperationException("Only the current Machine schema can be written.");
        if (!Guid.TryParse(document.Id, out _)) throw new InvalidOperationException("Machine ID must be a stable GUID.");
        if (string.IsNullOrWhiteSpace(document.DisplayName)) throw new InvalidOperationException("Machine display name is required.");
        RuntimeDefinitionValidation.Validate(document.DisplayName, document.Runtime);
        if (document.SurfaceAssignments.GroupBy(x => x.TargetId, StringComparer.Ordinal).Any(x => x.Count() > 1)) throw new InvalidOperationException("Machine surface target assignments must be unique.");
        if (document.ReelAssignments.GroupBy(x => x.MachineReelReference).Any(x => x.Count() > 1)) throw new InvalidOperationException("Machine reel assignments must be unique.");
        if (document.ReelAssignments.Any(x => x.MachineReelReference.Kind != MachineObjectKind.Reel || x.ReelAsset is null)) throw new InvalidOperationException("Machine reel assignments require a logical Reel reference and Reel asset reference.");
        if (document.ObjectInstances is null) throw new InvalidOperationException("Machine ObjectInstances collection is required.");
        if (document.ObjectInstances.GroupBy(x => x.Id, StringComparer.Ordinal).Any(x => x.Count() > 1)) throw new InvalidOperationException("Machine Object3D instance IDs must be unique (case-sensitive).");
        foreach (var instance in document.ObjectInstances)
        {
            if (!MachineObject3DInstanceId.IsValid(instance.Id)) throw new InvalidOperationException($"Machine Object3D instance ID '{instance.Id}' is invalid; use letters, digits, underscore, or hyphen.");
            if (string.IsNullOrWhiteSpace(instance.DisplayName)) throw new InvalidOperationException($"Machine Object3D instance '{instance.Id}' display name is required.");
            if (instance.ObjectAsset is null) throw new InvalidOperationException($"Machine Object3D instance '{instance.Id}' requires an Object3D asset reference.");
            _ = new AssetReference(instance.ObjectAsset.Scope, instance.ObjectAsset.Path);
            ValidateTransform(instance.Id, instance.Transform);
        }
        if (document.Anchors is null) throw new InvalidOperationException("Machine Anchors collection is required.");
        if (document.Anchors.GroupBy(x => x.Id, StringComparer.Ordinal).Any(x => x.Count() > 1)) throw new InvalidOperationException("Machine anchor IDs must be unique (case-sensitive).");
        foreach (var anchor in document.Anchors)
        {
            if (!MachineCompositionId.IsValid(anchor.Id)) throw new InvalidOperationException($"Machine anchor ID '{anchor.Id}' is invalid; use letters, digits, underscore, or hyphen.");
            if (string.IsNullOrWhiteSpace(anchor.DisplayName)) throw new InvalidOperationException($"Machine anchor '{anchor.Id}' display name is required.");
            if (anchor.Position is null || anchor.Rotation is null || !Finite(anchor.Position) || !Finite(anchor.Rotation))
                throw new InvalidOperationException($"Machine anchor '{anchor.Id}' position and rotation must be finite.");
        }
        if (document.CabinetAsset is not null) _ = new AssetReference(document.CabinetAsset.Scope, document.CabinetAsset.Path);
        foreach (var assignment in document.ReelAssignments) _ = new AssetReference(assignment.ReelAsset.Scope, assignment.ReelAsset.Path);
    }

    private static void ValidateTransform(string id, MachineObjectTransform? transform)
    {
        if (transform is null || transform.Position is null || transform.Rotation is null || transform.Scale is null)
            throw new InvalidOperationException($"Machine Object3D instance '{id}' requires a transform.");
        if (!Finite(transform.Position) || !Finite(transform.Rotation) || !Finite(transform.Scale))
            throw new InvalidOperationException($"Machine Object3D instance '{id}' transform values must be finite.");
        if (transform.Scale.X <= 0 || transform.Scale.Y <= 0 || transform.Scale.Z <= 0)
            throw new InvalidOperationException($"Machine Object3D instance '{id}' scale values must be positive.");
    }

    private static bool Finite(MachineVector3 value) => double.IsFinite(value.X) && double.IsFinite(value.Y) && double.IsFinite(value.Z);
}

public static class MachineCompositionId
{
    public static bool IsValid(string? id) => !string.IsNullOrWhiteSpace(id) && id.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');
}

public static class MachineObject3DInstanceId
{
    public static bool IsValid(string? id) => MachineCompositionId.IsValid(id);
}

public static class MachineStartupSelectionPolicy
{
    public static string? SelectAutomatic(IReadOnlyList<string> machineManifestPaths) => machineManifestPaths.Count == 1 ? machineManifestPaths[0] : null;
}

public static class MachineRuntimeSettingsBinding
{
    public static T CreateEditableSnapshot<T>(MachineDocument? machine) where T : class, new()
    {
        if (machine?.Runtime is not EmulationRuntimeDefinition { PlatformSettings: T settings }) return new T();
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(settings)) ?? new T();
    }
}
