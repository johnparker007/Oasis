using Oasis.Scripting;
using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.CabinetEditor.Services;
using SharpGLTF.Schema2;

namespace OasisEditor;

public sealed record OasisScriptMachineDiagnostic(
    string Code,
    OasisScriptDiagnosticSeverity Severity,
    string Message,
    string SourceName,
    OasisScriptTextSpan Span)
{
    public int Line => Span.Line;
    public int Column => Span.Column;
    public override string ToString() => $"{SourceName}:{Line}:{Column}: {Code} {Message}";
}

/// <summary>Canonical Machine-owned identities available to an authored behaviour.</summary>
public sealed record OasisScriptMachineReferenceIndex(
    IReadOnlySet<string> Objects,
    IReadOnlySet<string> Anchors,
    IReadOnlySet<string> Triggers,
    IReadOnlySet<string> Inputs,
    IReadOnlySet<string> Lamps,
    IReadOnlySet<string> Reels,
    IReadOnlySet<string> AlphaDisplays,
    IReadOnlySet<string> SevenSegmentDisplays)
{
    internal static OasisScriptMachineReferenceIndex FromMachineFields(MachineDocument machine) => new(
        machine.ObjectInstances.Select(value => value.Id).ToHashSet(StringComparer.Ordinal),
        machine.Anchors.Select(value => value.Id).ToHashSet(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal),
        machine.InputDefinitions.Select(value => value.Id).ToHashSet(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal),
        machine.ReelAssignments.Select(value => value.MachineReelReference.Id).ToHashSet(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal));
}

public sealed record OasisScriptMachineReferenceIndexBuildResult(
    OasisScriptMachineReferenceIndex References,
    IReadOnlyList<OasisScriptMachineDiagnostic> Diagnostics);

/// <summary>Resolves the logical reference domains of the assembled authored Machine.</summary>
public sealed class OasisScriptMachineReferenceIndexBuilder
{
    private readonly AssetReferenceResolver _assetResolver = new();

    public OasisScriptMachineReferenceIndexBuildResult Build(EditorProject? project, string libraryRoot, MachineDocument machine)
    {
        ArgumentNullException.ThrowIfNull(machine);
        var baseline = OasisScriptMachineReferenceIndex.FromMachineFields(machine);
        var triggers = new HashSet<string>(StringComparer.Ordinal);
        var lamps = new HashSet<string>(StringComparer.Ordinal);
        var alphas = new HashSet<string>(StringComparer.Ordinal);
        var sevenSegments = new HashSet<string>(StringComparer.Ordinal);
        var diagnostics = new List<OasisScriptMachineDiagnostic>();

        if (machine.CabinetAsset is not null)
        {
            if (project is null) AddResolutionDiagnostic(diagnostics, "OSM3101", "Cabinet references cannot be resolved because no Project is open.");
            else ResolveCabinet(project, libraryRoot, machine.CabinetAsset, triggers, diagnostics);
        }
        foreach (var assignment in machine.SurfaceAssignments)
        {
            if (project is null) { AddResolutionDiagnostic(diagnostics, "OSM3102", $"Face assigned to '{assignment.TargetId}' cannot be resolved because no Project is open."); continue; }
            ResolveFace(project, assignment, lamps, alphas, sevenSegments, diagnostics);
        }

        return new(new(baseline.Objects, baseline.Anchors, triggers, baseline.Inputs, lamps,
            baseline.Reels, alphas, sevenSegments), diagnostics);
    }

    private void ResolveCabinet(EditorProject project, string libraryRoot, AssetReference reference, ISet<string> triggers, ICollection<OasisScriptMachineDiagnostic> diagnostics)
    {
        try
        {
            var manifest = _assetResolver.Resolve(project, libraryRoot, reference);
            if (!File.Exists(manifest)) { AddResolutionDiagnostic(diagnostics, "OSM3101", $"Cabinet asset could not be resolved: '{reference.Path}'."); return; }
            if (!CabinetDocumentStorage.TryRead(File.ReadAllText(manifest), out CabinetDocument cabinet))
            { AddResolutionDiagnostic(diagnostics, "OSM3101", $"Cabinet asset is invalid: '{reference.Path}'."); return; }
            var modelPath = Path.IsPathFullyQualified(cabinet.Model.Path) ? cabinet.Model.Path : Path.Combine(Path.GetDirectoryName(manifest)!, cabinet.Model.Path);
            if (!File.Exists(modelPath)) { AddResolutionDiagnostic(diagnostics, "OSM3101", $"Cabinet GLB could not be resolved: '{cabinet.Model.Path}'."); return; }
            var model = ModelRoot.Load(modelPath);
            var scene = model.DefaultScene ?? model.LogicalScenes.FirstOrDefault();
            if (scene is null) return;
            foreach (var node in scene.VisualChildren) CollectTriggers(node, triggers);
        }
        catch (Exception exception)
        { AddResolutionDiagnostic(diagnostics, "OSM3101", $"Cabinet asset could not be resolved: {exception.Message}"); }
    }

    private static void CollectTriggers(Node node, ISet<string> triggers)
    {
        var id = CabinetSemanticGeometry.GetSemanticId(node.Name, node.Mesh?.Name, CabinetSemanticGeometryKind.Trigger);
        if (!string.IsNullOrWhiteSpace(id)) triggers.Add(id);
        foreach (var child in node.VisualChildren) CollectTriggers(child, triggers);
    }

    private static void ResolveFace(EditorProject project, MachineSurfaceAssignment assignment, ISet<string> lamps, ISet<string> alphas, ISet<string> sevenSegments, ICollection<OasisScriptMachineDiagnostic> diagnostics)
    {
        try
        {
            var path = new ProjectAssetPathService().ResolveProjectRelativePath(project, assignment.FaceAssetPath);
            if (!File.Exists(path)) { AddResolutionDiagnostic(diagnostics, "OSM3102", $"Face assigned to '{assignment.TargetId}' could not be resolved: '{assignment.FaceAssetPath}'."); return; }
            if (!FaceDocumentStorage.TryReadValidated(File.ReadAllText(path), out var file, out var error))
            { AddResolutionDiagnostic(diagnostics, "OSM3102", $"Face assigned to '{assignment.TargetId}' is invalid: {error}"); return; }
            foreach (var reference in FaceDocumentStorage.ToModel(file).Elements
                         .Where(element => element.LinkedMachineObjectReference.HasValue)
                         .Select(element => element.LinkedMachineObjectReference!.Value))
            {
                if (reference.Kind == MachineObjectKind.Lamp) lamps.Add(reference.Id);
                else if (reference.Kind == MachineObjectKind.AlphaDisplay) alphas.Add(reference.Id);
                else if (reference.Kind == MachineObjectKind.SevenSegmentDisplay) sevenSegments.Add(reference.Id);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { AddResolutionDiagnostic(diagnostics, "OSM3102", $"Face assigned to '{assignment.TargetId}' could not be resolved: {exception.Message}"); }
    }

    private static void AddResolutionDiagnostic(ICollection<OasisScriptMachineDiagnostic> diagnostics, string code, string message) =>
        diagnostics.Add(new(code, OasisScriptDiagnosticSeverity.Error, message, MachineBehaviorDefinition.CanonicalSourcePath, new(0, 0, 1, 1)));
}

/// <summary>Second-stage validation; the pure Oasis language remains unaware of Machine composition.</summary>
public static class OasisScriptMachineValidator
{
    public static IReadOnlyList<OasisScriptMachineDiagnostic> Validate(
        OasisScriptProgram program,
        OasisScriptMachineReferenceIndex references,
        string sourceName = MachineBehaviorDefinition.CanonicalSourcePath)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(references);
        var diagnostics = new List<OasisScriptMachineDiagnostic>();
        Visit(program.Syntax, expression =>
        {
            if (expression is not OasisScriptReferenceLiteralSyntax literal) return;
            var (set, code, domain) = literal.Type.Kind switch
            {
                OasisScriptTypeKind.ObjectRef => (references.Objects, "OSM3001", "Object3D instance"),
                OasisScriptTypeKind.AnchorRef => (references.Anchors, "OSM3002", "anchor"),
                OasisScriptTypeKind.TriggerRef => (references.Triggers, "OSM3003", "trigger"),
                OasisScriptTypeKind.InputRef => (references.Inputs, "OSM3004", "input"),
                OasisScriptTypeKind.LampRef => (references.Lamps, "OSM3005", "lamp"),
                OasisScriptTypeKind.ReelRef => (references.Reels, "OSM3006", "reel"),
                OasisScriptTypeKind.AlphaDisplayRef => (references.AlphaDisplays, "OSM3007", "alpha display"),
                OasisScriptTypeKind.SevenSegmentRef => (references.SevenSegmentDisplays, "OSM3008", "seven-segment display"),
                _ => (null, string.Empty, string.Empty)
            };
            if (set is not null && !set.Contains(literal.Id))
                diagnostics.Add(new(code, OasisScriptDiagnosticSeverity.Error, $"Unknown {domain} '{literal.Id}'.", sourceName, literal.Span));
        });
        return diagnostics;
    }

    private static void Visit(OasisScriptSyntaxNode node, Action<OasisScriptExpressionSyntax> expressionVisitor)
    {
        if (node is OasisScriptExpressionSyntax expression) expressionVisitor(expression);
        switch (node)
        {
            case OasisScriptProgramSyntax value: foreach (var child in value.Declarations) Visit(child, expressionVisitor); break;
            case OasisScriptVariableDeclarationSyntax value: Visit(value.Initializer, expressionVisitor); break;
            case OasisScriptEventHandlerSyntax value:
                foreach (var parameter in value.Parameters) Visit(parameter, expressionVisitor);
                Visit(value.Body, expressionVisitor); break;
            case OasisScriptEventFilterSyntax value: Visit(value.Value, expressionVisitor); break;
            case OasisScriptBlockSyntax value: foreach (var child in value.Statements) Visit(child, expressionVisitor); break;
            case OasisScriptLetStatementSyntax value: Visit(value.Initializer, expressionVisitor); break;
            case OasisScriptAssignmentStatementSyntax value: Visit(value.Value, expressionVisitor); break;
            case OasisScriptIfStatementSyntax value:
                Visit(value.Condition, expressionVisitor); Visit(value.ThenBlock, expressionVisitor);
                if (value.ElseBlock is not null) Visit(value.ElseBlock, expressionVisitor); break;
            case OasisScriptForStatementSyntax value: Visit(value.Iterable, expressionVisitor); Visit(value.Body, expressionVisitor); break;
            case OasisScriptExpressionStatementSyntax value: Visit(value.Expression, expressionVisitor); break;
            case OasisScriptListExpressionSyntax value: foreach (var child in value.Elements) Visit(child, expressionVisitor); break;
            case OasisScriptIndexExpressionSyntax value: Visit(value.Target, expressionVisitor); Visit(value.Index, expressionVisitor); break;
            case OasisScriptUnaryExpressionSyntax value: Visit(value.Operand, expressionVisitor); break;
            case OasisScriptBinaryExpressionSyntax value: Visit(value.Left, expressionVisitor); Visit(value.Right, expressionVisitor); break;
            case OasisScriptBuiltinCallExpressionSyntax value: foreach (var child in value.Arguments) Visit(child, expressionVisitor); break;
        }
    }
}

public sealed record OasisScriptEditorDiagnostic(string Severity, string Code, string Message, int Line, int Column, int Offset);
