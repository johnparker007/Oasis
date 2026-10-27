using SkiaSharp;

namespace OasisEditor;

internal readonly record struct FaceSourceShapeOutputSize(int Width, int Height);

/// <summary>WPF compatibility boundary for the portable source-shape transform shared with Avalonia.</summary>
internal static class PortableFaceSourceShapeTransformAdapter
{
    public static FaceSourceShapeOutputSize EstimateRegisteredImageOutputSize(int sourceWidth, int sourceHeight, FacePerspectiveRegistrationModel registration)
    {
        var size = FacePipeline.FaceSourceShapeTransformService.EstimateRegisteredImageOutputSize(sourceWidth, sourceHeight, registration);
        return new(size.Width, size.Height);
    }

    public static FaceSourceShapeOutputSize EstimateOutputSize(PanelFaceSourceShapeModel shape, double? targetAspectRatio = null)
    {
        var size = FacePipeline.FaceSourceShapeTransformService.EstimateOutputSize(shape, targetAspectRatio);
        return new(size.Width, size.Height);
    }

    public static string? TryGenerateBackground(Panel2DDocumentModel panel, PanelFaceSourceShapeModel shape, int width, int height,
        string? projectDirectory, string? outputPath = null)
        => FacePipeline.FaceSourceShapeTransformService.TryGenerateBackground(panel, shape, width, height, projectDirectory, outputPath);

    internal static SKBitmap? TryGenerateBackgroundBitmap(Panel2DDocumentModel panel, PanelFaceSourceShapeModel shape,
        int width, int height, string projectDirectory)
        => FacePipeline.FaceSourceShapeTransformService.TryGenerateBackgroundBitmap(panel, shape, width, height, projectDirectory);

    public static string? TryGenerateTransformedElementAsset(PanelElementModel sourceElement, string? sourceAssetPath,
        PanelFaceSourceShapeModel shape, int faceWidth, int faceHeight, FaceSourceRegionModel faceBounds,
        string? projectDirectory, string fileNamePrefix)
        => FacePipeline.FaceSourceShapeTransformService.TryGenerateTransformedElementAsset(sourceElement, sourceAssetPath,
            shape, faceWidth, faceHeight, faceBounds, projectDirectory, fileNamePrefix);

    public static bool TryTransformFacePointToPanel(PanelFaceSourceShapeModel shape, int width, int height,
        double faceX, double faceY, out FacePointModel panelPoint)
        => FacePipeline.FaceSourceShapeTransformService.TryTransformFacePointToPanel(shape, width, height, faceX, faceY, out panelPoint);

    public static bool TryTransformPanelPointToFace(PanelFaceSourceShapeModel shape, int width, int height,
        double panelX, double panelY, out FacePointModel facePoint)
        => FacePipeline.FaceSourceShapeTransformService.TryTransformPanelPointToFace(shape, width, height, panelX, panelY, out facePoint);

    public static bool ContainsPanelPoint(PanelFaceSourceShapeModel shape, double panelX, double panelY)
        => FacePipeline.FaceSourceShapeTransformService.ContainsPanelPoint(shape, panelX, panelY);

    internal static bool TryCreateFaceToPanelHomography(PanelFaceSourceShapeModel shape, int width, int height, out double[] h)
        => FacePipeline.FaceSourceShapeTransformService.TryCreateFaceToPanelHomography(shape, width, height, out h);

    internal static FacePointModel[] CreateFaceCorners(int width, int height)
        => FacePipeline.FaceSourceShapeTransformService.CreateFaceCorners(width, height);

    internal static bool TryCreateHomography(IReadOnlyList<FacePointModel> source, IReadOnlyList<FacePointModel> destination, out double[] h)
        => FacePipeline.FaceSourceShapeTransformService.TryCreateHomography(source, destination, out h);

    internal static bool TryApplyHomography(double[] h, double sourceX, double sourceY, out FacePointModel point)
        => FacePipeline.FaceSourceShapeTransformService.TryApplyHomography(h, sourceX, sourceY, out point);
}
