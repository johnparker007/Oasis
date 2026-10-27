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
    MachineAnchor[] Anchors,
    MachineBehaviorDefinition? Behavior)
{
    public const int CurrentSchemaVersion = 11;

    public MachinePrefabDeclaration[] PrefabDependencies { get; init; } = [];
    public MachineGeometrySurface[] GeometrySurfaces { get; init; } = [];
    public MachineInteractionDefinition Interaction { get; init; } = new("Buttons");

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
        [],
        null);
}

/// <summary>Authored player interaction mode and its optional Pool-specific references.</summary>
public sealed record MachineInteractionDefinition(string Kind, string? CuePrefabAlias = null, string? PlayingSurfaceId = null);

/// <summary>The single optional behaviour source owned by a Machine package.</summary>
public sealed record MachineBehaviorDefinition(string Kind, string Source)
{
    public const string OasisScriptKind = "OasisScript";
    public const string CanonicalSourcePath = "behavior.oasis";
    public static MachineBehaviorDefinition OasisScript() => new(OasisScriptKind, CanonicalSourcePath);
}

/// <summary>A three-dimensional value in Machine authoring coordinates.</summary>
public sealed record MachineVector3(double X, double Y, double Z)
{
    public static MachineVector3 Zero { get; } = new(0, 0, 0);
    public static MachineVector3 One { get; } = new(1, 1, 1);
}

/// <summary>
/// A Machine-level transform. Rotation is authored as Euler angles in degrees around X, Y and Z;
/// runtime conversion applies them in Unity's standard Z-X-Y order. Instance transforms are parent-local.
/// </summary>
public sealed record MachineObjectTransform(MachineVector3 Position, MachineVector3 Rotation, MachineVector3 Scale)
{
    public static MachineObjectTransform Identity { get; } = new(MachineVector3.Zero, MachineVector3.Zero, MachineVector3.One);
}

/// <summary>One independently identified use of a reusable Object3D asset in a Machine.</summary>
public sealed record MachineObject3DInstance(string Id, string DisplayName, AssetReference? ObjectAsset, MachineObjectTransform Transform,
    string? ParentId = null, bool Active = true);

/// <summary>A stable, Machine-owned position and XYZ Euler rotation in composition space. Anchors have no scale or scene object.</summary>
public sealed record MachineAnchor(string Id, string DisplayName, MachineVector3 Position, MachineVector3 Rotation)
{
    public static MachineAnchor Create(string id, string displayName = "Anchor") => new(id, displayName, MachineVector3.Zero, MachineVector3.Zero);
}

/// <summary>A named reusable Object3D Prefab available to Machine behaviour, including spawn-only Prefabs.</summary>
public sealed record MachinePrefabDeclaration(string Alias, AssetReference PrefabAsset);

/// <summary>A named oriented rectangle authored in Machine coordinates for focused geometry queries.</summary>
public sealed record MachineGeometrySurface(string Id, MachineVector3 Position, MachineVector3 Rotation, double Width, double Length);

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

/// <summary>Oasis owns this Machine's behaviour directly.</summary>
public sealed record OasisRuntimeDefinition : RuntimeDefinition
{
    public const string RuntimeKind = "Oasis";
    public override string Kind => RuntimeKind;
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
    public static void Validate(string machineName, RuntimeDefinition runtime, MachineBehaviorDefinition? behavior = null)
    {
        if (runtime is OasisRuntimeDefinition)
        {
            if (behavior is null || behavior.Kind != MachineBehaviorDefinition.OasisScriptKind || behavior.Source != MachineBehaviorDefinition.CanonicalSourcePath)
                throw new InvalidOperationException($"Machine '{machineName}' Runtime 'Oasis' requires OasisScript behaviour at behavior.oasis.");
            return;
        }
        if (runtime is not EmulationRuntimeDefinition emulation)
            throw new InvalidOperationException($"Machine '{machineName}' has unsupported Runtime '{runtime?.Kind ?? "(missing)"}'.");
        if (behavior is not null)
            throw new InvalidOperationException($"Machine '{machineName}' Runtime 'Emulation' must not declare behaviour.");
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
            writer.WritePropertyName("prefabDependencies"); JsonSerializer.Serialize(writer, document.PrefabDependencies, Options);
            writer.WritePropertyName("geometrySurfaces"); JsonSerializer.Serialize(writer, document.GeometrySurfaces, Options);
            writer.WritePropertyName("interaction"); JsonSerializer.Serialize(writer, document.Interaction, Options);
            writer.WritePropertyName("anchors"); JsonSerializer.Serialize(writer, document.Anchors, Options);
            if (document.Behavior is not null) { writer.WritePropertyName("behavior"); JsonSerializer.Serialize(writer, document.Behavior, Options); }
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
                case OasisRuntimeDefinition oasis:
                    writer.WriteString("kind", oasis.Kind);
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
            RuntimeDefinition definition;
            var kind = runtime.GetProperty("kind").GetString();
            if (kind == OasisRuntimeDefinition.RuntimeKind) definition = new OasisRuntimeDefinition();
            else if (kind == EmulationRuntimeDefinition.RuntimeKind)
            {
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
                definition = new EmulationRuntimeDefinition(platform, settings);
            }
            else { error = $"Unsupported Machine runtime kind '{kind}'."; return false; }
            document = new MachineDocument(
                version.GetInt32(), root.GetProperty("id").GetString() ?? string.Empty,
                root.GetProperty("displayName").GetString() ?? string.Empty,
                root.TryGetProperty("cabinetAsset", out var cabinet) ? cabinet.Deserialize<AssetReference>(Options) : null,
                root.TryGetProperty("surfaceAssignments", out var surfaces) ? surfaces.Deserialize<MachineSurfaceAssignment[]>(Options) ?? [] : [],
                root.TryGetProperty("reelAssignments", out var reels) ? reels.Deserialize<MachineReelAssignment[]>(Options) ?? [] : [],
                root.GetProperty("objectInstances").Deserialize<MachineObject3DInstance[]>(Options) ?? throw new InvalidOperationException("Machine ObjectInstances collection is required."),
                definition,
                root.TryGetProperty("inputDefinitions", out var inputs) ? inputs.Deserialize<List<InputDefinitionModel>>(Options) ?? [] : [],
                root.GetProperty("anchors").Deserialize<MachineAnchor[]>(Options) ?? throw new InvalidOperationException("Machine Anchors collection is required."),
                root.TryGetProperty("behavior", out var behavior) ? behavior.Deserialize<MachineBehaviorDefinition>(Options) : null)
            {
                PrefabDependencies = root.TryGetProperty("prefabDependencies", out var dependencies)
                    ? dependencies.Deserialize<MachinePrefabDeclaration[]>(Options) ?? [] : [],
                GeometrySurfaces = root.TryGetProperty("geometrySurfaces", out var geometrySurfaces)
                    ? geometrySurfaces.Deserialize<MachineGeometrySurface[]>(Options) ?? [] : [],
                Interaction = root.TryGetProperty("interaction", out var interaction)
                    ? interaction.Deserialize<MachineInteractionDefinition>(Options) ?? throw new InvalidOperationException("Machine Interaction object is required.")
                    : throw new InvalidOperationException("Machine Interaction object is required.")
            };
            Validate(document);
            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException or NotSupportedException or KeyNotFoundException)
        { error = $"Invalid Machine document: {exception.Message}"; return false; }
    }

    internal static void Validate(MachineDocument document)
    {
        if (document.SchemaVersion != MachineDocument.CurrentSchemaVersion) throw new InvalidOperationException("Only the current Machine schema can be written.");
        if (!Guid.TryParse(document.Id, out _)) throw new InvalidOperationException("Machine ID must be a stable GUID.");
        if (string.IsNullOrWhiteSpace(document.DisplayName)) throw new InvalidOperationException("Machine display name is required.");
        if (document.InputDefinitions is null || document.InputDefinitions.Any(input => input is null))
            throw new InvalidOperationException("Machine InputDefinitions collection and entries are required.");
        if (document.InputDefinitions.GroupBy(input => input.Id, StringComparer.Ordinal).Any(group => group.Count() > 1))
            throw new InvalidOperationException("Machine input IDs must be unique (case-sensitive).");
        foreach (var input in document.InputDefinitions)
        {
            if (string.IsNullOrWhiteSpace(input.Id) || string.IsNullOrWhiteSpace(input.Name))
                throw new InvalidOperationException("Each Machine input requires a stable ID and display name.");
            if (!Enum.IsDefined(input.Kind)) throw new InvalidOperationException($"Machine input '{input.Id}' has an unknown kind.");
            if (input.CoinChannel is < 0 || input.CoinValue is < 0)
                throw new InvalidOperationException($"Machine input '{input.Id}' coin channel and value cannot be negative.");
        }
        ValidateInteraction(document);
        RuntimeDefinitionValidation.Validate(document.DisplayName, document.Runtime, document.Behavior);
        if (document.SurfaceAssignments.GroupBy(x => x.TargetId, StringComparer.Ordinal).Any(x => x.Count() > 1)) throw new InvalidOperationException("Machine surface target assignments must be unique.");
        if (document.ReelAssignments.GroupBy(x => x.MachineReelReference).Any(x => x.Count() > 1)) throw new InvalidOperationException("Machine reel assignments must be unique.");
        if (document.ReelAssignments.Any(x => x.MachineReelReference.Kind != MachineObjectKind.Reel || x.ReelAsset is null)) throw new InvalidOperationException("Machine reel assignments require a logical Reel reference and Reel asset reference.");
        if (document.ObjectInstances is null) throw new InvalidOperationException("Machine ObjectInstances collection is required.");
        if (document.ObjectInstances.Any(i => i is null)) throw new InvalidOperationException("Machine ObjectInstances cannot contain null entries.");
        if (document.ObjectInstances.GroupBy(x => x.Id, StringComparer.Ordinal).Any(x => x.Count() > 1)) throw new InvalidOperationException("Machine Object3D instance IDs must be unique (case-sensitive).");
        foreach (var instance in document.ObjectInstances)
        {
            if (!MachineObject3DInstanceId.IsValid(instance.Id)) throw new InvalidOperationException($"Machine Object3D instance ID '{instance.Id}' is invalid; use letters, digits, underscore, or hyphen.");
            if (string.IsNullOrWhiteSpace(instance.DisplayName)) throw new InvalidOperationException($"Machine Object3D instance '{instance.Id}' display name is required.");
            if (instance.ObjectAsset is null) throw new InvalidOperationException($"Machine Object3D instance '{instance.Id}' requires an Object3D asset reference.");
            _ = new AssetReference(instance.ObjectAsset.Scope, instance.ObjectAsset.Path);
            ValidateTransform(instance.Id, instance.Transform);
        }
        ValidateInstanceParents(document.ObjectInstances);
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
        if (document.PrefabDependencies is null) throw new InvalidOperationException("Machine PrefabDependencies collection is required.");
        if (document.PrefabDependencies.Any(dependency => dependency is null)) throw new InvalidOperationException("Machine PrefabDependencies cannot contain null entries.");
        if (document.PrefabDependencies.GroupBy(dependency => dependency.Alias, StringComparer.Ordinal).Any(group => group.Count() > 1))
            throw new InvalidOperationException("Machine Prefab aliases must be unique (case-sensitive).");
        foreach (var dependency in document.PrefabDependencies)
        {
            if (!MachineCompositionId.IsValid(dependency.Alias)) throw new InvalidOperationException($"Machine Prefab alias '{dependency.Alias}' is invalid; use letters, digits, underscore, or hyphen.");
            if (dependency.PrefabAsset is null) throw new InvalidOperationException($"Machine Prefab alias '{dependency.Alias}' requires an asset reference.");
            _ = new AssetReference(dependency.PrefabAsset.Scope, dependency.PrefabAsset.Path);
        }
        if (document.GeometrySurfaces is null) throw new InvalidOperationException("Machine GeometrySurfaces collection is required.");
        if (document.GeometrySurfaces.Any(surface => surface is null)) throw new InvalidOperationException("Machine GeometrySurfaces cannot contain null entries.");
        if (document.GeometrySurfaces.GroupBy(surface => surface.Id, StringComparer.Ordinal).Any(group => group.Count() > 1))
            throw new InvalidOperationException("Machine geometry surface IDs must be unique (case-sensitive).");
        foreach (var surface in document.GeometrySurfaces)
        {
            if (!MachineCompositionId.IsValid(surface.Id)) throw new InvalidOperationException($"Machine geometry surface ID '{surface.Id}' is invalid; use letters, digits, underscore, or hyphen.");
            if (surface.Position is null || surface.Rotation is null || !Finite(surface.Position) || !Finite(surface.Rotation))
                throw new InvalidOperationException($"Machine geometry surface '{surface.Id}' position and rotation must be finite.");
            if (!double.IsFinite(surface.Width) || surface.Width <= 0 || !double.IsFinite(surface.Length) || surface.Length <= 0)
                throw new InvalidOperationException($"Machine geometry surface '{surface.Id}' width and length must be positive and finite.");
        }
    }

    private static void ValidateInteraction(MachineDocument document)
    {
        var interaction = document.Interaction ?? throw new InvalidOperationException("Machine Interaction object is required.");
        if (interaction.Kind == "Buttons")
        {
            if (interaction.CuePrefabAlias is not null || interaction.PlayingSurfaceId is not null)
                throw new InvalidOperationException("Buttons interaction must not declare Pool parameters.");
            return;
        }
        if (interaction.Kind != "Pool") throw new InvalidOperationException($"Unsupported Machine interaction kind '{interaction.Kind}'. Use Buttons or Pool.");
        if (string.IsNullOrWhiteSpace(interaction.CuePrefabAlias) || !MachineCompositionId.IsValid(interaction.CuePrefabAlias))
            throw new InvalidOperationException("Pool interaction requires a valid CuePrefabAlias.");
        if (!document.PrefabDependencies.Any(dependency => string.Equals(dependency.Alias, interaction.CuePrefabAlias, StringComparison.Ordinal)))
            throw new InvalidOperationException($"Pool interaction CuePrefabAlias '{interaction.CuePrefabAlias}' must name a declared Prefab dependency.");
        if (string.IsNullOrWhiteSpace(interaction.PlayingSurfaceId) || !MachineCompositionId.IsValid(interaction.PlayingSurfaceId))
            throw new InvalidOperationException("Pool interaction requires a valid PlayingSurfaceId.");
        if (!document.GeometrySurfaces.Any(surface => string.Equals(surface.Id, interaction.PlayingSurfaceId, StringComparison.Ordinal)))
            throw new InvalidOperationException($"Pool interaction PlayingSurfaceId '{interaction.PlayingSurfaceId}' must name a declared geometry surface.");
    }

    internal static void ValidateInstanceParents(MachineObject3DInstance[] objects)
    {
        var instances = objects.ToDictionary(i => i.Id, StringComparer.Ordinal);
        foreach (var instance in objects)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal) { instance.Id };
            var current = instance;
            while (current.ParentId is not null)
            {
                if (!instances.TryGetValue(current.ParentId, out var parent)) throw new InvalidOperationException($"Machine instance '{current.Id}' parent '{current.ParentId}' does not exist.");
                if (!visited.Add(parent.Id)) throw new InvalidOperationException($"Machine instance '{instance.Id}' has a parent cycle.");
                current = parent;
            }
        }
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
