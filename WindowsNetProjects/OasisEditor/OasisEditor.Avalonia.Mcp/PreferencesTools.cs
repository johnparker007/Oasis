using System.ComponentModel;
using ModelContextProtocol.Server;

namespace OasisEditor.Avalonia.Mcp;

public sealed partial class EditorTools
{
    [McpServerTool(Name = "inspect_preferences", ReadOnly = true), Description("Inspect Avalonia Editor user preferences, CPU image-processing workers, and Oasis Player launch settings.")]
    public Task<object> InspectPreferences(CancellationToken cancellationToken) => ui.Invoke(() =>
    {
        var preferences = capture.View.GetPreferences();
        return (object)new
        {
            preferences,
            availableLogicalProcessors = capture.View.AvailableLogicalProcessors,
            effectiveProcessingWorkers = capture.View.EffectiveProcessingWorkers
        };
    }, cancellationToken);

    [McpServerTool(Name = "save_preferences"), Description("Opt in to saving preferences in a file inside the explicit MCP workspace, then persist CPU processing and Oasis Player launch settings there. No personal AppData preferences are read or written in headless mode.")]
    public Task<object> SavePreferences(string path, AvaloniaEditorPreferences preferences, CancellationToken cancellationToken)
        => ui.Invoke(() =>
        {
            capture.View.ConfigurePreferencesPersistence(path);
            capture.View.UpdatePreferences(preferences);
            var saved = capture.View.GetPreferences();
            return (object)new
            {
                preferences = saved,
                availableLogicalProcessors = capture.View.AvailableLogicalProcessors,
                effectiveProcessingWorkers = capture.View.EffectiveProcessingWorkers
            };
        }, cancellationToken);
}
