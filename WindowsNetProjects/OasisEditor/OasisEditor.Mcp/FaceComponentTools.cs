using System.ComponentModel;
using ModelContextProtocol.Server;

namespace OasisEditor.Avalonia.Mcp;

public sealed partial class EditorTools
{
    [McpServerTool(Name = "add_face_button"), Description("Create and select a native Face button. Optionally assign its explicit Machine input ID.")]
    public Task<object> AddFaceButton(string name, double x, double y, CancellationToken cancellationToken,
        double width = 80, double height = 40, string? inputId = null)
        => ui.Invoke(() => ActFace(() => faces.AddButton(name, x, y, width, height, inputId)), cancellationToken);

    [McpServerTool(Name = "add_face_seven_segment_display"), Description("Create and select a native Face seven-segment display. A machine display index is optional and is never guessed.")]
    public Task<object> AddFaceSevenSegmentDisplay(string name, double x, double y, CancellationToken cancellationToken,
        double width = 100, double height = 48, int? displayIndex = null, int digitCount = 1)
        => ui.Invoke(() => ActFace(() => faces.AddSevenSegmentDisplay(name, x, y, width, height, displayIndex, digitCount)), cancellationToken);

    [McpServerTool(Name = "add_face_alpha_display"), Description("Create and select a native Face alpha display. A machine display index is optional and is never guessed.")]
    public Task<object> AddFaceAlphaDisplay(string name, double x, double y, CancellationToken cancellationToken,
        double width = 220, double height = 48, int? displayIndex = null, int digitCount = 16)
        => ui.Invoke(() => ActFace(() => faces.AddAlphaDisplay(name, x, y, width, height, displayIndex, digitCount)), cancellationToken);

    [McpServerTool(Name = "update_face_component"), Description("Apply one undoable typed edit to a native Face button, seven-segment display or alpha display. Optional fields preserve their current values; Clear flags remove nullable fields.")]
    public Task<object> UpdateFaceComponent(string id, FaceComponentUpdateOptions options, CancellationToken cancellationToken)
        => ui.Invoke(() => ActFace(() => faces.UpdateFaceComponent(id, options)), cancellationToken);

    [McpServerTool(Name = "inspect_face_component", ReadOnly = true), Description("Inspect one native Face button or display, including its authored metadata and settings.")]
    public Task<object> InspectFaceComponent(string id, CancellationToken cancellationToken)
        => ui.Invoke<object>(() =>
        {
            var element = faces.Document?.Elements.FirstOrDefault(candidate => string.Equals(candidate.ObjectId, id, StringComparison.Ordinal));
            if (element is not (FaceButtonElement or FaceSevenSegmentDisplayElement or FaceAlphaDisplayElement))
                throw new KeyNotFoundException($"Face button or display '{id}' was not found.");
            return new { workspace = Snapshot(), component = element };
        }, cancellationToken);

    [McpServerTool(Name = "update_face_generation_settings"), Description("Update native Face artwork sharpening, lamp-mask threshold and tray-generation settings as one undoable edit.")]
    public Task<object> UpdateFaceGenerationSettings(FaceGenerationSettingsModel settings, CancellationToken cancellationToken)
        => ui.Invoke(() => ActFace(() => faces.UpdateGenerationSettings(settings)), cancellationToken);
}
