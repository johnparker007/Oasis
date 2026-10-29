using Avalonia.Controls;
using Avalonia.Media;

namespace OasisEditor.Avalonia;

public sealed record MachineInputDiagnostic(string Code, string Message, string InputId, string Severity = "Warning");

/// <summary>Checks only authored declarations and visual IDs supplied by the active workspace documents.</summary>
public static class MachineInputDiagnosticsAnalyzer
{
    public static IReadOnlyList<MachineInputDiagnostic> Analyze(
        IReadOnlyList<InputDefinitionModel> inputs,
        IReadOnlySet<Guid>? visibleElementIds = null,
        bool visualContextAvailable = false)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var diagnostics = new InputMapDiagnosticsService()
            .Analyze(FruitMachinePlatformType.None, inputs)
            .Select(diagnostic => new MachineInputDiagnostic(diagnostic.Code, diagnostic.Message, diagnostic.InputDefinitionId,
                diagnostic.Severity.ToString()))
            .ToList();

        if (visualContextAvailable)
        {
            var ids = visibleElementIds ?? new HashSet<Guid>();
            foreach (var input in inputs.Where(input => input.LinkedVisualElementId.HasValue && !ids.Contains(input.LinkedVisualElementId.Value)))
                diagnostics.Add(new("input.unresolved_visual", $"Linked visual '{input.LinkedVisualElementId!.Value:D}' was not found in the currently open Panel2D or Face.", input.Id));
        }

        return diagnostics;
    }
}

public sealed partial class WorkspaceView
{
    /// <summary>Current Machine input diagnostics, scoped to visual IDs in the open Panel2D and Face.</summary>
    public IReadOnlyList<MachineInputDiagnostic> GetMachineInputDiagnostics()
    {
        var machine = Machines.Document;
        if (machine is null) return [];
        var ids = new HashSet<Guid>();
        AddIds(_session.HierarchyElements.Select(element => element.Id), ids);
        var panels=DocumentHost._nativeDocuments.Values.Where(d=>d.Asset.Kind=="Panel2D").ToArray();
        foreach(var panel in panels)AddIds(panel.Asset.Sessions.Project.HierarchyElements.Select(e=>e.Id),ids);
        AddIds(_session.SourceShapes.Select(shape => shape.Id), ids);
        AddIds(Faces.Elements.Select(element => element.Id), ids);
        var hasVisualContext = _session.PanelPath is not null || Faces.FacePath is not null || panels.Length>0;
        return MachineInputDiagnosticsAnalyzer.Analyze(machine.InputDefinitions, ids, hasVisualContext);
    }

    /// <summary>Builds a diagnostics section for the Machine Inspector; call after rendering its input fields.</summary>
    public Control CreateMachineInputDiagnosticsControl()
    {
        var diagnostics = GetMachineInputDiagnostics();
        var panel = new StackPanel { Spacing = 4, Margin = new global::Avalonia.Thickness(0, 8, 0, 0) };
        panel.Children.Add(new TextBlock { Text = $"INPUT MAP DIAGNOSTICS · {diagnostics.Count} warning(s)", FontSize = 11, FontWeight = global::Avalonia.Media.FontWeight.SemiBold });
        if (diagnostics.Count == 0)
        {
            var contextOpen = _session.PanelPath is not null || Faces.FacePath is not null;
            panel.Children.Add(new TextBlock
            {
                Text = contextOpen ? "No input map diagnostics." : "No shortcut conflicts. Open the related Panel2D or Face to check visual links.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = .72
            });
        }
        else
        {
            foreach (var diagnostic in diagnostics)
                panel.Children.Add(new TextBlock { Text = $"[{diagnostic.Severity}] {diagnostic.Message}", TextWrapping = TextWrapping.Wrap });
        }
        return panel;
    }

    private static void AddIds(IEnumerable<string> values, ISet<Guid> destination)
    {
        foreach (var value in values)
            if (Guid.TryParse(value, out var id)) destination.Add(id);
    }
}
