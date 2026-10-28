using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace OasisEditor.Avalonia.Mcp;

public sealed partial class EditorTools
{
    [McpServerTool(Name = "inspect_menus", ReadOnly = true), Description("Inspect actual Editor menu hierarchy, enabled/check state and shortcut labels.")]
    public Task<EditorMenuEntry[]> InspectMenus(CancellationToken cancellationToken) => ui.Invoke(capture.View.InspectMenus, cancellationToken);

    [McpServerTool(Name = "capture_menu", ReadOnly = true), Description("Capture actual open menu popups in the headless workspace. Path is e.g. [File,Create] or [Emulation]. No desktop interaction.")]
    public Task<CallToolResult> CaptureMenu(string[] path, CancellationToken cancellationToken, string theme = "Dark") => ui.Invoke(() =>
    {
        capture.Capture(theme: theme);
        try
        {
            capture.View.OpenMenuForCapture(path);
            return new CallToolResult { Content = [ImageContentBlock.FromBytes(capture.Capture(theme: theme), "image/png")] };
        }
        finally { capture.View.CloseMenus(); }
    }, cancellationToken);
}
