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
    RuntimeDefinition Runtime,
    List<InputDefinitionModel> InputDefinitions)
{
    public const int CurrentSchemaVersion = 4;

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
        EmulationRuntimeDefinition.Create(FruitMachinePlatformType.None),
        []);
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
                new EmulationRuntimeDefinition(platform, settings),
                root.TryGetProperty("inputDefinitions", out var inputs) ? inputs.Deserialize<List<InputDefinitionModel>>(Options) ?? [] : []);
            Validate(document);
            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException or NotSupportedException)
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
        if (document.CabinetAsset is not null) _ = new AssetReference(document.CabinetAsset.Scope, document.CabinetAsset.Path);
        foreach (var assignment in document.ReelAssignments) _ = new AssetReference(assignment.ReelAsset.Scope, assignment.ReelAsset.Path);
    }
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
