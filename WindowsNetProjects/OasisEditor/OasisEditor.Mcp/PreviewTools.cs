using System.ComponentModel;
using ModelContextProtocol.Server;

namespace OasisEditor.Avalonia.Mcp;

public sealed partial class EditorTools
{
    [McpServerTool(Name = "start_preview"), Description("Start REAL Fabric/Amber emulation from the open saved Machine. Explicit native libraries must be installed for its authored platform. Headless preview silently drains actual PCM; it never opens a desktop window or audio device. Refuses dirty documents or replacing a running preview.")]
    public Task<NativePreviewSnapshot> StartPreview(NativeEmulationPreferences preferences, CancellationToken cancellationToken)
        => ui.InvokeAsync(async () =>
        {
            capture.View.ConfigureNativePreview(preferences);
            return await capture.View.StartNativePreviewAsync(cancellationToken: cancellationToken);
        }, cancellationToken);

    [McpServerTool(Name = "inspect_preview", ReadOnly = true), Description("Inspect actual backend lifecycle, throttle, failure and consumed native PCM counters. Native execution and PCM counters do not establish audible playback or correct rendering.")]
    public Task<NativePreviewSnapshot> InspectPreview(CancellationToken cancellationToken)
        => ui.Invoke(capture.View.InspectNativePreview, cancellationToken);

    [McpServerTool(Name = "control_preview"), Description("Control the owned real emulation preview: pause, resume, reset, throttle, unthrottle or stop. Pause/reset/stop release held inputs. Stop remains available after the authored source changes.")]
    public Task<NativePreviewSnapshot> ControlPreview(string action, CancellationToken cancellationToken)
        => ui.InvokeAsync(async () =>
        {
            await capture.View.Preview.ControlAsync(action, cancellationToken);
            return capture.View.InspectNativePreview();
        }, cancellationToken);

    [McpServerTool(Name = "preview_input"), Description("Press/release one input by its exact authored Machine input ID. Coins use the real separate Fabric coin protocol and can be rejected. No mappings or credit/spin behavior are simulated; repeated key-down does not insert repeated coins.")]
    public Task<object> PreviewInput(string id, bool pressed, CancellationToken cancellationToken)
        => ui.InvokeAsync<object>(async () => new
        {
            result = (await capture.View.Preview.SetInputAsync(id, pressed, cancellationToken))?.ToString(),
            preview = capture.View.InspectNativePreview()
        }, cancellationToken);
}
