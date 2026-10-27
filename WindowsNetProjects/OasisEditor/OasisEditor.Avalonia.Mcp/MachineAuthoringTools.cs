using System.Globalization;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace OasisEditor.Avalonia.Mcp;

public sealed partial class EditorTools
{
    private MachineAuthoringSession Machines => capture.View.Machines;

    [McpServerTool(Name = "list_machines"), Description("List native schema-11 Machine manifests in the open project.")]
    public Task<object> ListMachines(CancellationToken cancellationToken)
        => ui.Invoke<object>(() => new { machines = Machines.ListMachines() }, cancellationToken);

    [McpServerTool(Name = "create_machine"), Description("Create a real native schema-11 Machine package in the open project. Refuses existing packages and unsaved document replacement.")]
    public Task<object> CreateMachine(string name, CancellationToken cancellationToken)
        => ui.Invoke(() => { var path = Machines.CreateMachine(name); capture.View.ShowMachine(); return MachineSnapshot(path); }, cancellationToken);

    [McpServerTool(Name = "open_machine"), Description("Open a current-schema Machine package through the shared storage validator. Confined to Assets/Machines and rejects dirty replacement.")]
    public Task<object> OpenMachine(string path, CancellationToken cancellationToken)
        => ui.Invoke(() => { Machines.OpenMachine(path); capture.View.ShowMachine(); return MachineSnapshot(); }, cancellationToken);

    [McpServerTool(Name = "inspect_machine", ReadOnly = true), Description("Inspect the complete authored Machine document, runtime/platform settings, inputs, interaction, assignments, prefabs, anchors and dirty/history state.")]
    public Task<object> InspectMachine(CancellationToken cancellationToken)
        => ui.Invoke<object>(() => MachineSnapshot(), cancellationToken);

    [McpServerTool(Name = "update_machine_document"), Description("Replace the active Machine with one complete typed schema-11 document edit. Full metadata is validated before the atomic undoable commit; omit fields only when the current schema defaults permit it.")]
    public Task<object> UpdateMachineDocument(MachineDocument document, CancellationToken cancellationToken)
        => ui.Invoke(() => ActMachine(() => Machines.UpdateDocument(_ => document)), cancellationToken);

    [McpServerTool(Name = "set_machine_name"), Description("Change the authored Machine display name as one undoable, validated edit.")]
    public Task<object> SetMachineName(string displayName, CancellationToken cancellationToken)
        => ui.Invoke(() => ActMachine(() => Machines.SetDisplayName(displayName)), cancellationToken);

    [McpServerTool(Name = "set_machine_runtime"), Description("Set runtime to Emulation on a currently supported platform with native default settings, or to Oasis with its canonical behavior source declaration. Game mappings are never inferred.")]
    public Task<object> SetMachineRuntime(string kind, CancellationToken cancellationToken, string platform = "None")
        => ui.Invoke(() => ActMachine(() =>
        {
            if (string.Equals(kind, "Oasis", StringComparison.OrdinalIgnoreCase)) Machines.SetRuntime(new OasisRuntimeDefinition());
            else if (string.Equals(kind, "Emulation", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<FruitMachinePlatformType>(platform, true, out var parsed) && EmulationRuntimePlatforms.IsSupported(parsed))
                Machines.SetRuntime(EmulationRuntimeDefinition.Create(parsed));
            else throw new ArgumentException("Choose Runtime Oasis or Emulation and one of the currently supported platforms.");
        }), cancellationToken);

    [McpServerTool(Name = "set_machine_platform_setting"), Description("Edit one typed platform setting by property path such as ProgramRom1Path, Reels[0].Steps or Coins[1].Enabled. Value is a JSON scalar matching the destination field type. One validated undoable change.")]
    public Task<object> SetMachinePlatformSetting(string propertyPath, string valueJson, CancellationToken cancellationToken)
        => ui.Invoke(() => ActMachine(() =>
        {
            var current = Machines.Document?.Runtime as EmulationRuntimeDefinition
                ?? throw new InvalidOperationException("The active Machine does not use Emulation runtime.");
            var settings = JsonSerializer.Deserialize(JsonSerializer.Serialize(current.PlatformSettings), current.PlatformSettings.GetType())!;
            SetNestedValue(settings, propertyPath, valueJson);
            Machines.UpdateDocument(document => document with { Runtime = new EmulationRuntimeDefinition(current.Platform, settings) });
        }), cancellationToken);

    [McpServerTool(Name = "set_machine_interaction"), Description("Set the player interaction mode to Buttons or Pool with explicit prefab and playing-surface IDs. IDs must reference authored declarations.")]
    public Task<object> SetMachineInteraction(string kind, CancellationToken cancellationToken, string? cuePrefabAlias = null, string? playingSurfaceId = null)
        => ui.Invoke(() => ActMachine(() => Machines.SetInteraction(string.Equals(kind, "Pool", StringComparison.OrdinalIgnoreCase)
            ? new MachineInteractionDefinition("Pool", cuePrefabAlias, playingSurfaceId)
            : string.Equals(kind, "Buttons", StringComparison.OrdinalIgnoreCase) ? new MachineInteractionDefinition("Buttons")
            : throw new ArgumentException("Interaction kind must be Buttons or Pool."))), cancellationToken);

    [McpServerTool(Name = "set_machine_behavior_source"), Description("Set the canonical behavior.oasis source only on an Oasis Machine. The real Oasis.Scripting compiler validates the edit before it enters history.")]
    public Task<object> SetMachineBehaviorSource(string source, CancellationToken cancellationToken)
        => ui.Invoke(() => ActMachine(() => Machines.SetOasisBehaviorSource(source)), cancellationToken);

    [McpServerTool(Name = "set_machine_inputs"), Description("Replace Machine input definitions using typed current-schema records as one undoable edit.")]
    public Task<object> SetMachineInputs(InputDefinitionModel[] inputs, CancellationToken cancellationToken)
        => ui.Invoke(() => ActMachine(() => Machines.SetInputs(inputs)), cancellationToken);

    [McpServerTool(Name = "apply_recovered_fml_inputs"), Description("Intentionally copy the last decoded FML input definitions into the active Machine through one undoable command. FML imports alone never alter Machine documents.")]
    public Task<object> ApplyRecoveredFmlInputs(CancellationToken cancellationToken)
        => ui.Invoke(() => ActMachine(() => Machines.ApplyRecoveredInputs(session.ImportedInputDefinitions)), cancellationToken);

    [McpServerTool(Name = "machine_history"), Description("Undo one Machine settings command; set redo=true to redo. Does not save.")]
    public Task<object> MachineHistory(CancellationToken cancellationToken, bool redo = false)
        => ui.Invoke(() => ActMachine(() => { if (redo) Machines.Redo(); else Machines.Undo(); }), cancellationToken);

    [McpServerTool(Name = "save_machine"), Description("Atomically save the current schema-11 Machine manifest and behavior source, refusing external changes to either authored file.")]
    public Task<object> SaveMachine(CancellationToken cancellationToken)
        => ui.Invoke(() => ActMachine(Machines.Save), cancellationToken);

    private object MachineSnapshot(string? path = null) => new
    {
        project = session.ProjectFilePath,
        path = path ?? Machines.MachinePath,
        dirty = Machines.IsDirty,
        canUndo = Machines.CanUndo,
        canRedo = Machines.CanRedo,
        document = Machines.Document is { } machine ? JsonDocument.Parse(MachineDocumentStorage.Serialize(machine)).RootElement.Clone() : (JsonElement?)null,
        behaviorSource = Machines.BehaviorSource,
        supportedPlatforms = EmulationRuntimePlatforms.Supported
    };

    private object ActMachine(Action action)
    {
        action(); capture.View.ShowMachine();
        return MachineSnapshot();
    }

    private static void SetNestedValue(object target, string path, string valueJson)
    {
        var parts = path.Replace("[", ".").Replace("]", "").Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) throw new ArgumentException("Setting property path is required.");
        object current = target;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i];
            if (int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                current = current is System.Collections.IList list && index >= 0 && index < list.Count ? list[index]! : throw new ArgumentException($"List index '{index}' is unavailable.");
            else current = current.GetType().GetProperty(part, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(current)
                ?? throw new ArgumentException($"Setting member '{part}' was not found.");
        }
        var final = parts[^1];
        if (int.TryParse(final, NumberStyles.None, CultureInfo.InvariantCulture, out var finalIndex))
        {
            if (current is not System.Collections.IList list || finalIndex < 0 || finalIndex >= list.Count) throw new ArgumentException($"List index '{finalIndex}' is unavailable.");
            var type = list[finalIndex]!.GetType(); list[finalIndex] = JsonSerializer.Deserialize(valueJson, type, JsonOptions())!; return;
        }
        var property = current.GetType().GetProperty(final, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (property?.CanWrite != true) throw new ArgumentException($"Writable setting '{final}' was not found.");
        property.SetValue(current, JsonSerializer.Deserialize(valueJson, property.PropertyType, JsonOptions()));
    }

    private static JsonSerializerOptions JsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
