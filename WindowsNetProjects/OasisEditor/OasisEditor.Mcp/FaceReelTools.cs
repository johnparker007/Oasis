using System.ComponentModel;
using ModelContextProtocol.Server;

namespace OasisEditor.Avalonia.Mcp;

public sealed partial class EditorTools
{
    [McpServerTool(Name = "add_face_reel_mount"), Description("Add a native Face reel mount with an explicitly supplied logical reel index. This authors a visual mount and does not guess or assign machine-specific reel mappings.")]
    public Task<object> AddFaceReelMount(string name, double x, double y, double width, double height, int reelIndex, CancellationToken cancellationToken)
        => ui.Invoke(() => ActFace(() => faces.AddReelMount(name, x, y, width, height, reelIndex)), cancellationToken);

    [McpServerTool(Name = "update_face_reel_mount"), Description("Update one Face reel mount's logical reel reference, symbol-band image, stops, scale, offset, direction, lamp settings, opacity, transmission mask or placement. Null option values leave fields unchanged; Clear flags clear nullable fields.")]
    public Task<object> UpdateFaceReelMount(string id, FaceReelMountUpdateOptions options, CancellationToken cancellationToken)
        => ui.Invoke(() => ActFace(() => faces.UpdateReelMount(id, options)), cancellationToken);

    [McpServerTool(Name = "import_face_reel_symbol_band"), Description("Copy a workspace image into the active Face package and assign it as a reel mount's symbol-band image. If the reel is opaque, also generate its transmission mask. The image is not synthesized or replaced with placeholder artwork.")]
    public Task<object> ImportFaceReelSymbolBand(string id, string path, CancellationToken cancellationToken)
        => ui.Invoke(() => ActFace(() => faces.ImportSymbolBand(id, ResolveWorkspaceFile(path))), cancellationToken);

    [McpServerTool(Name = "generate_face_reel_transmission_mask"), Description("Generate and assign a transmission mask from a reel mount's existing symbol-band image. The band is kept unchanged; the new mask is a separate authored Face package asset and can be undone/redone.")]
    public Task<object> GenerateFaceReelTransmissionMask(string id, CancellationToken cancellationToken)
        => ui.Invoke(() => ActFace(() => faces.GenerateTransmissionMask(id)), cancellationToken);

    [McpServerTool(Name = "inspect_face_reel_mount", ReadOnly = true), Description("Inspect one native Face reel mount, including logical reel reference, symbol-band path, geometry, stops, direction, reel lamps, opacity and transmission-mask path.")]
    public Task<object> InspectFaceReelMount(string id, CancellationToken cancellationToken)
        => ui.Invoke<object>(() =>
        {
            var mount = faces.Document?.Elements.OfType<FaceReelMount>()
                .FirstOrDefault(element => string.Equals(element.ObjectId, id, StringComparison.Ordinal));
            if (mount is null) throw new KeyNotFoundException($"Face reel mount '{id}' was not found.");
            return new { workspace = Snapshot(), reelMount = mount };
        }, cancellationToken);
}
