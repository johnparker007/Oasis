using System.IO;
using SharpGLTF.Schema2;

namespace OasisEditor.Features.CabinetEditor.Services;

public sealed record CabinetTriggerIdentity(string Id, string SourceName, string? Diagnostic)
{
    public string ScriptReference => $"trigger:{Id}";
    public bool IsValid => Diagnostic is null;
}

/// <summary>Transient projection of the selected GLB scene; never authored or persisted.</summary>
public sealed record CabinetTriggerInventory(bool IsAvailable, IReadOnlyList<CabinetTriggerIdentity> Triggers, string? Error)
{
    public string Status => !IsAvailable ? $"Cabinet/model unavailable: {Error}"
        : Triggers.Count == 0 ? "No trigger geometry discovered."
        : $"Discovered {Triggers.Count} named trigger(s); {Triggers.Count(value => !value.IsValid)} invalid or ambiguous.";

    public static CabinetTriggerInventory Unavailable(string error) => new(false, [], error);

    public static CabinetTriggerInventory Read(string modelPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath)) return Unavailable("GLB could not be resolved.");
            var model = ModelRoot.Load(modelPath);
            var scene = model.DefaultScene ?? model.LogicalScenes.FirstOrDefault();
            return scene is null ? Unavailable("GLB has no scene.") : Discover(scene);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { return Unavailable(exception.Message); }
    }

    public static CabinetTriggerInventory Discover(Scene scene)
    {
        var declarations = new List<CabinetTriggerIdentity>();
        foreach (var node in scene.VisualChildren) Collect(node, declarations);
        var duplicateIds = declarations.GroupBy(value => value.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        return new(true, declarations.Select(value => duplicateIds.Contains(value.Id)
            ? value with { Diagnostic = $"Duplicate Cabinet trigger ID '{value.Id}'." } : value).ToArray(), null);
    }

    private static void Collect(Node node, ICollection<CabinetTriggerIdentity> declarations)
    {
        var id = CabinetSemanticGeometry.GetSemanticId(node.Name, node.Mesh?.Name, CabinetSemanticGeometryKind.Trigger);
        if (id is not null)
            declarations.Add(new(id, CabinetSemanticGeometry.GetWinningSemanticName(node.Name, node.Mesh?.Name)!,
                !MachineCompositionId.IsValid(id) ? "Empty or invalid logical trigger ID."
                : node.Mesh is null ? "Trigger does not reference a mesh." : null));
        foreach (var child in node.VisualChildren) Collect(child, declarations);
    }
}
