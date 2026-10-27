using System.ComponentModel;
using ModelContextProtocol.Server;

namespace OasisEditor.Avalonia.Mcp;

public sealed partial class EditorTools
{
    [McpServerTool(Name = "inspect_machine_input_diagnostics", ReadOnly = true), Description("Analyze the active Machine's authored keyboard shortcuts and linked visual IDs. Visual links are checked only against the currently open Panel2D and Face; no platform bindings are inferred.")]
    public Task<object> InspectMachineInputDiagnostics(CancellationToken cancellationToken)
        => ui.Invoke<object>(() => new
        {
            project = session.ProjectFilePath,
            machine = capture.View.Machines.MachinePath,
            diagnostics = capture.View.GetMachineInputDiagnostics(),
            visualScope = "Currently open Panel2D and Face documents"
        }, cancellationToken);
    [McpServerTool(Name="configure_recent_projects"), Description("Configure the welcome page using an explicit recent-project settings file inside this workspace. Headless sessions never access personal recents.")]
    public Task<object> ConfigureRecentProjects(string path, CancellationToken cancellationToken)
        => ui.Invoke<object>(() => {
            var full = Path.GetFullPath(path, session.WorkspacePath);
            full = WorkspacePathPolicy.ResolveConfined(session.WorkspacePath, Path.GetRelativePath(session.WorkspacePath, full));
            capture.View.ConfigureWelcomePage(new RecentProjectService(full));
            return new { path = full };
        }, cancellationToken);
}
