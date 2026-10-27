using System.ComponentModel;
using ModelContextProtocol.Server;

namespace OasisEditor.Avalonia.Mcp;

public sealed partial class EditorTools
{
    [McpServerTool(Name = "inspect_panel_element_properties", ReadOnly = true), Description("Inspect editable WPF-compatible asset, background colour and label lamp-number properties for one Panel2D element.")]
    public Task<object> InspectPanelElementProperties(string id, CancellationToken cancellationToken)
        => ui.Invoke<object>(() => new { id, properties = session.GetPanelElementInspectorSettings(id) }, cancellationToken);

    [McpServerTool(Name = "update_panel_element_properties"), Description("Apply one undoable update to Panel2D asset paths, background colour or label lamp number. Asset paths must remain inside the open project; blank paths clear assignments.")]
    public Task<object> UpdatePanelElementProperties(string id, PanelElementInspectorSettings settings, CancellationToken cancellationToken)
        => ui.Invoke(() => Act(() => session.UpdatePanelElementInspectorSettings(id, settings)), cancellationToken);
}
