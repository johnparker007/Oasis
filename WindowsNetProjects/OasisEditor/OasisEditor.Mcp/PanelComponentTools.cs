using System.ComponentModel;
using ModelContextProtocol.Server;

namespace OasisEditor.Avalonia.Mcp;

public sealed partial class EditorTools
{
    [McpServerTool(Name = "add_panel_component"), Description("Add a production-native Panel2D lamp, reel, seven-segment display, segment-alpha display or VFD dot matrix with WPF-compatible defaults. One undoable edit; does not save.")]
    public Task<object> AddPanelComponent(string kind, double x, double y, CancellationToken cancellationToken)
        => ui.Invoke(() => Act(() => session.AddSpecialistElement(kind, x, y)), cancellationToken);

    [McpServerTool(Name = "inspect_panel_component",ReadOnly=true), Description("Inspect native settings for a selected-kind Panel2D lamp, reel, seven-segment, alpha or VFD component.")]
    public Task<object> InspectPanelComponent(string id, CancellationToken cancellationToken)
        => ui.Invoke<object>(() => new { id, settings=session.GetSpecialistSettings(id) }, cancellationToken);

    [McpServerTool(Name = "update_panel_component"), Description("Edit the native settings of a Panel2D specialist component through one undoable history entry. Omitted fields are retained from current settings.")]
    public Task<object> UpdatePanelComponent(string id, CancellationToken cancellationToken,
        int? displayNumber = null, string? onColorHex = null, string? offColorHex = null, string? textColorHex = null,
        string? displayText = null, bool? hasBorder = null, string? segmentDisplayType = null, bool? showDecimalPoint = null,
        bool? showCommaTail = null, int? stops = null, double? visibleScale = null, double? bandOffset = null,
        bool? isReversed = null, bool? reelLampsEnabled = null, int? topLampNumber = null, int? middleLampNumber = null,
        int? bottomLampNumber = null)
        => ui.Invoke(() => Act(() =>
        {
            var current = session.GetSpecialistSettings(id) ?? throw new InvalidOperationException("The selected element has no specialist settings.");
            session.UpdateSpecialistSettings(id, new PanelSpecialistSettings(
                displayNumber ?? current.DisplayNumber, onColorHex ?? current.OnColorHex, offColorHex ?? current.OffColorHex,
                textColorHex ?? current.TextColorHex, displayText ?? current.DisplayText, hasBorder ?? current.HasBorder,
                segmentDisplayType ?? current.SegmentDisplayType, showDecimalPoint ?? current.ShowDecimalPoint,
                showCommaTail ?? current.ShowCommaTail, stops ?? current.Stops, visibleScale ?? current.VisibleScale,
                bandOffset ?? current.BandOffset, isReversed ?? current.IsReversed, reelLampsEnabled ?? current.ReelLampsEnabled,
                topLampNumber ?? current.TopLampNumber, middleLampNumber ?? current.MiddleLampNumber, bottomLampNumber ?? current.BottomLampNumber));
        }), cancellationToken);
}
