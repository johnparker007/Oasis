using System.ComponentModel;
using ModelContextProtocol.Server;

namespace OasisEditor.Avalonia.Mcp;

public sealed partial class EditorTools
{
    [McpServerTool(Name = "generate_face_from_source_shape"), Description("Create a complete native Face from a saved Panel2D Face source region. Runs perspective correction, mask extraction, semantic lamp/button/display/reel conversion and tray authoring through the shared Core generation service.")]
    public Task<object> GenerateFaceFromSourceShape(string sourceShapeId, string title, CancellationToken cancellationToken,
        double? targetAspectRatio = null)
        => ui.Invoke(() => ActFace(() => faces.CreateFromPanelFaceSourceShape(sourceShapeId, title, targetAspectRatio,
            cancellationToken: cancellationToken)), cancellationToken);

    [McpServerTool(Name = "import_face_artwork"), Description("Copy a workspace image into the active Face package, apply its saved perspective registration, and build Correction Input, Base and Output assets.")]
    public Task<object> ImportFaceArtwork(string path, CancellationToken cancellationToken)
        => ui.Invoke(() =>
        {
            var source = ResolveWorkspaceFile(path);
            return ActFace(() => faces.ImportArtwork(source));
        }, cancellationToken);

    [McpServerTool(Name = "import_face_artwork_override"), Description("Copy a workspace image into the active Face package as a registered artwork override. The Base artwork remains separately generated.")]
    public Task<object> ImportFaceArtworkOverride(string path, CancellationToken cancellationToken)
        => ui.Invoke(() =>
        {
            var source = ResolveWorkspaceFile(path);
            return ActFace(() => faces.ImportArtworkOverride(source));
        }, cancellationToken);

    [McpServerTool(Name = "set_face_artwork_registration"), Description("Set the source image's normalized perspective quadrilateral in top-left, top-right, bottom-right, bottom-left order. A valid convex quad is required.")]
    public Task<object> SetFaceArtworkRegistration(double topLeftX, double topLeftY, double topRightX, double topRightY,
        double bottomRightX, double bottomRightY, double bottomLeftX, double bottomLeftY, CancellationToken cancellationToken)
        => ui.Invoke(() => ActFace(() => faces.SetArtworkRegistration(new FacePerspectiveRegistrationModel
        {
            TopLeft = new() { X = topLeftX, Y = topLeftY }, TopRight = new() { X = topRightX, Y = topRightY },
            BottomRight = new() { X = bottomRightX, Y = bottomRightY }, BottomLeft = new() { X = bottomLeftX, Y = bottomLeftY }
        })), cancellationToken);

    [McpServerTool(Name = "set_face_artwork_calibration"), Description("Replace the calibration operation in the Face processing pipeline. References may contain sampled regions or manual colours; enabled state and spatial/tonal options are saved into the native Face recipe.")]
    public Task<object> SetFaceArtworkCalibration(ArtworkCalibrationOperationModel calibration, CancellationToken cancellationToken)
        => ui.Invoke(() => ActFace(() => faces.SetArtworkCalibration(calibration)), cancellationToken);

    [McpServerTool(Name = "remove_face_artwork_calibration"), Description("Remove the calibration operation from the Face artwork processing pipeline through undoable document history.")]
    public Task<object> RemoveFaceArtworkCalibration(CancellationToken cancellationToken)
        => ui.Invoke(() => ActFace(() => faces.SetArtworkCalibration(null)), cancellationToken);

    [McpServerTool(Name = "set_face_artwork_override_alignment"), Description("Position the registered override over Base artwork and choose which source supplies alpha. Override geometry is validated and stored in the native Face recipe.")]
    public Task<object> SetFaceArtworkOverrideAlignment(double x, double y, double width, double height,
        FaceArtworkOverrideAlphaSource alphaSource, CancellationToken cancellationToken,
        double topLeftX = 0, double topLeftY = 0, double topRightX = 1, double topRightY = 0,
        double bottomRightX = 1, double bottomRightY = 1, double bottomLeftX = 0, double bottomLeftY = 1,
        bool enabled = true)
        => ui.Invoke(() => ActFace(() => faces.SetArtworkOverrideAlignment(x, y, width, height, alphaSource,
            new FacePerspectiveRegistrationModel
            {
                TopLeft = new() { X = topLeftX, Y = topLeftY }, TopRight = new() { X = topRightX, Y = topRightY },
                BottomRight = new() { X = bottomRightX, Y = bottomRightY }, BottomLeft = new() { X = bottomLeftX, Y = bottomLeftY }
            }, enabled)), cancellationToken);

    [McpServerTool(Name = "rebuild_face_artwork"), Description("Rebuild Correction Input, calibrated Base and final Output from the saved image or Panel2D source, registration, processing pipeline and override recipe.")]
    public Task<object> RebuildFaceArtwork(CancellationToken cancellationToken)
        => ui.Invoke(() => ActFace(() => faces.RebuildArtwork(cancellationToken)), cancellationToken);

    [McpServerTool(Name = "inspect_face_artwork", ReadOnly = true), Description("Inspect the saved Face artwork source, registration, processing operations, override settings and generated stage paths.")]
    public Task<object> InspectFaceArtwork(CancellationToken cancellationToken)
        => ui.Invoke<object>(() => new { workspace = Snapshot(), artwork = FaceDocumentStorage.ToFile(new FaceDocumentModel { Artwork = faces.CurrentArtwork }).Artwork }, cancellationToken);

    private string ResolveWorkspaceFile(string path)
    {
        var fullPath = Path.GetFullPath(path, session.WorkspacePath);
        var relative = Path.GetRelativePath(session.WorkspacePath, fullPath);
        var resolved = WorkspacePathPolicy.ResolveConfined(session.WorkspacePath, relative, requireExists: true);
        WorkspacePathPolicy.RejectReparsePoints(session.WorkspacePath, Path.GetRelativePath(session.WorkspacePath, resolved), requireExists: true);
        if (!File.Exists(resolved)) throw new FileNotFoundException("The workspace image was not found.", resolved);
        return resolved;
    }
}
