using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
namespace OasisEditor.Avalonia.Mcp;
public sealed partial class EditorTools
{
    private object OutputState() => new { entries = capture.View.Output.Entries.ToArray(), filteredEntries = capture.View.Output.FilteredEntries, capture.View.Output.DiskLogPath };
    [McpServerTool(Name="inspect_output", ReadOnly=true), Description("Inspect the isolated workspace's bounded Output entries and active filter results.")]
    public Task<object> InspectOutput(CancellationToken cancellationToken) => ui.Invoke(OutputState,cancellationToken);
    [McpServerTool(Name="filter_output"), Description("Filter the Output pane by text and severity without changing its stored entries.")]
    public Task<object> FilterOutput(CancellationToken cancellationToken, string searchText="",bool showInfo=true,bool showWarnings=true,bool showErrors=true)
        => ui.Invoke(() => { var o=capture.View.Output; o.SearchText=searchText;o.ShowInfo=showInfo;o.ShowWarnings=showWarnings;o.ShowErrors=showErrors; return OutputState(); },cancellationToken);
    [McpServerTool(Name="show_output"), Description("Activate the docked Output tab in the isolated workspace.")]
    public Task<object> ShowOutput(CancellationToken cancellationToken) => ui.Invoke(() => {capture.View.ShowOutputPane();return OutputState();},cancellationToken);
}
