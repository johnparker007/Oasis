using System.ComponentModel;
using ModelContextProtocol.Server;

namespace OasisEditor.Avalonia.Mcp;

public sealed partial class EditorTools
{
    [McpServerTool(Name = "inspect_machine_composition_graph", ReadOnly = true), Description("Build a read-only graph from the active Machine's saved Cabinet, Face, Panel2D, Reel and Prefab references, including unresolved dependency diagnostics.")]
    public Task<MachineOverview> InspectMachineCompositionGraph(CancellationToken cancellationToken)
        => ui.Invoke(capture.View.InspectMachineCompositionGraph, cancellationToken);

    [McpServerTool(Name = "show_machine_composition_graph"), Description("Show or hide the Machine's composition graph alongside its 3D scene.")]
    public Task<MachineOverview> ShowMachineCompositionGraph(bool visible, CancellationToken cancellationToken)
        => ui.Invoke(() =>
        {
            capture.View.ShowMachineCompositionGraph(visible);
            return capture.View.InspectMachineCompositionGraph();
        }, cancellationToken);

    [McpServerTool(Name = "open_machine_composition_node"), Description("Open a saved asset represented by one node in the active Machine composition graph.")]
    public Task<object> OpenMachineCompositionNode(string id, CancellationToken cancellationToken)
        => ui.InvokeAsync<object>(async () =>
        {
            await capture.View.ActivateMachineCompositionNodeAsync(id);
            return Snapshot();
        }, cancellationToken);
}
