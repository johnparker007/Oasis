using Oasis.Scripting;

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
    public static OasisScriptMachineReferenceIndex FromMachine(MachineDocument machine) => new(
        machine.ObjectInstances.Select(value => value.Id).ToHashSet(StringComparer.Ordinal),
        machine.Anchors.Select(value => value.Id).ToHashSet(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal),
        machine.InputDefinitions.Select(value => value.Id).ToHashSet(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal),
        machine.ReelAssignments.Select(value => value.MachineReelReference.Id).ToHashSet(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal));
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
