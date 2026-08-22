namespace OasisEditor;

public enum CalibrationSamplingMode
{
    Pixel,
    Area
}

public enum CalibrationPlacementTargetKind { BlackReference, WhiteReference, SameColorGroup }

public sealed record CalibrationPlacementState(string OperationId, CalibrationPlacementTargetKind TargetKind,
    string TargetId, CalibrationSamplingMode SamplingMode, double RadiusNormalized);

public sealed class FaceDocumentModel
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string? SourcePanel2DDocumentId { get; init; }
    public string? SourcePanel2DDocumentPath { get; init; }
    public string? SourceFaceShapeId { get; init; }
    public string? AssignedCabinetFaceTargetId { get; init; }
    public string? AssignedCabinetAssetPath { get; init; }
    public FaceSourceRegionModel? SourceRegion { get; init; }
    public DateTime? LastRegeneratedAtUtc { get; init; }
    public FaceGenerationSettingsModel GenerationSettings { get; init; } = FaceGenerationSettingsModel.Default;
    public FaceArtworkModel? Artwork { get; init; }
    public FaceRuntimeRenderAssetsModel? RuntimeRenderAssets { get; init; }
    public FaceMaskLayerModel? MaskLayer { get; init; }
    public IReadOnlyList<FaceTrayModel> Trays { get; init; } = [];
    public IReadOnlyList<FaceLampEmitterElement> LampEmitters { get; init; } = [];
    public IReadOnlyList<FaceLayerModel> Layers { get; init; } = [];
    public IReadOnlyList<FaceElementModel> Elements { get; init; } = [];
}

public enum FaceArtworkSourceKind
{
    Panel2DFaceSourceShape,
    IndependentImage
}

/// <summary>Authored artwork state owned by the Face. GeneratedAssetPath is rebuildable output.</summary>
public sealed class FaceArtworkModel
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public FaceArtworkSourceModel Source { get; init; } = new();
    public ImageProcessingPipelineModel ProcessingPipeline { get; init; } = new();
    public string? GeneratedAssetPath { get; init; }
    public int OutputWidth { get; init; }
    public int OutputHeight { get; init; }
}

public sealed class FaceArtworkSourceModel
{
    public FaceArtworkSourceKind Kind { get; init; } = FaceArtworkSourceKind.Panel2DFaceSourceShape;
    public string? AssetPath { get; init; }
    public string? Panel2DDocumentId { get; init; }
    public string? Panel2DDocumentPath { get; init; }
    public string? FaceSourceShapeId { get; init; }
}

public sealed class ImageProcessingPipelineModel
{
    public IReadOnlyList<ImageProcessingOperationModel> Operations { get; init; } = [];
}

public abstract class ImageProcessingOperationModel
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public abstract ImageProcessingOperationKind Kind { get; }
    public bool Enabled { get; init; } = true;
}

public enum ImageProcessingOperationKind
{
    ArtworkCalibration
}

public sealed class CalibrationSampleModel
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public double X { get; init; }
    public double Y { get; init; }
    public CalibrationSamplingMode SamplingMode { get; init; }
    public double RadiusNormalized { get; init; } = .01d;
    public CalibrationSampleModel Normalize() => new() { Id = string.IsNullOrWhiteSpace(Id) ? Guid.NewGuid().ToString("N") : Id.Trim(), X = double.IsFinite(X) ? Math.Clamp(X, 0, 1) : 0, Y = double.IsFinite(Y) ? Math.Clamp(Y, 0, 1) : 0, SamplingMode = SamplingMode, RadiusNormalized = double.IsFinite(RadiusNormalized) ? Math.Clamp(RadiusNormalized, 0, .5) : .01d };
    public double RadiusPixels(int width, int height) => RadiusNormalized * Math.Min(width, height);
    public CalibrationSampleModel WithRadiusPixels(double pixels, int width, int height) => new() { Id = Id, X = X, Y = Y, SamplingMode = SamplingMode, RadiusNormalized = Math.Max(0, pixels) / Math.Max(1, Math.Min(width, height)) };
}

public sealed class CalibrationReferenceModel
{
    public IReadOnlyList<CalibrationSampleModel> Samples { get; init; } = [];
    public bool ManualEnabled { get; init; }
    public string ManualColor { get; init; } = "#FFFFFFFF";
}

public sealed class SameColorCalibrationGroupModel
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "Colour Group";
    public IReadOnlyList<CalibrationSampleModel> Samples { get; init; } = [];
}

public sealed class ArtworkCalibrationOperationModel : ImageProcessingOperationModel
{
    public const double DefaultStrength = 100d;

    public override ImageProcessingOperationKind Kind => ImageProcessingOperationKind.ArtworkCalibration;
    public double Strength { get; init; } = DefaultStrength;
    public CalibrationReferenceModel BlackReference { get; init; } = new() { ManualColor = "#FF000000" };
    public CalibrationReferenceModel WhiteReference { get; init; } = new();
    public IReadOnlyList<SameColorCalibrationGroupModel> SameColorGroups { get; init; } = [];
    public bool CorrectSpatialBrightness { get; init; } = true;
    public bool CorrectSpatialColor { get; init; } = true;
    public bool NormalizeBlackWhite { get; init; } = true;
    public bool NeutralizeWhite { get; init; } = true;

    public ArtworkCalibrationOperationModel Normalize() => new()
    {
        Id = string.IsNullOrWhiteSpace(Id) ? Guid.NewGuid().ToString("N") : Id.Trim(),
        Enabled = Enabled,
        Strength = double.IsFinite(Strength) ? Math.Clamp(Strength, 0d, 100d) : DefaultStrength,
        BlackReference = NormalizeReference(BlackReference, "#FF000000"), WhiteReference = NormalizeReference(WhiteReference, "#FFFFFFFF"),
        SameColorGroups = SameColorGroups.Select(g => new SameColorCalibrationGroupModel { Id = string.IsNullOrWhiteSpace(g.Id) ? Guid.NewGuid().ToString("N") : g.Id.Trim(), Name = string.IsNullOrWhiteSpace(g.Name) ? "Colour Group" : g.Name.Trim(), Samples = g.Samples.Select(s => s.Normalize()).ToArray() }).ToArray(),
        CorrectSpatialBrightness = CorrectSpatialBrightness, CorrectSpatialColor = CorrectSpatialColor,
        NormalizeBlackWhite = NormalizeBlackWhite, NeutralizeWhite = NeutralizeWhite
    };

    private static CalibrationReferenceModel NormalizeReference(CalibrationReferenceModel value, string fallback) => new() { Samples = value.Samples.Select(s => s.Normalize()).ToArray(), ManualEnabled = value.ManualEnabled, ManualColor = NormalizeColor(value.ManualColor, fallback) };

    private static string NormalizeColor(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var text = value.Trim();
        if (text.StartsWith('#')) text = text[1..];
        return text.Length is 6 or 8 && text.All(Uri.IsHexDigit) ? $"#{text.ToUpperInvariant()}" : fallback;
    }
}

public sealed class NormalizedFacePointModel
{
    public double X { get; init; }
    public double Y { get; init; }

    public NormalizedFacePointModel Normalize() => new()
    {
        X = double.IsFinite(X) ? Math.Clamp(X, 0d, 1d) : 0d,
        Y = double.IsFinite(Y) ? Math.Clamp(Y, 0d, 1d) : 0d
    };
}

public sealed class FaceGenerationSettingsModel
{
    public const bool DefaultPostWarpSharpeningEnabled = true;
    public const double DefaultPostWarpSharpeningAmount = 0.65d;
    public const double DefaultPostWarpSharpeningRadiusPixels = 0.75d;
    public const int DefaultPostWarpSharpeningThreshold = 2;
    public const byte DefaultMaskExtractionThreshold = 1;
    public const double DefaultTrayBoundsInflationPercent = 0d;
    public const double DefaultTrayBoundsPaddingPixels = 0d;
    public const bool DefaultClampTrayBoundsToLampWindow = false;

    public static FaceGenerationSettingsModel Default { get; } = new();

    public byte MaskExtractionThreshold { get; init; } = DefaultMaskExtractionThreshold;
    public double TrayBoundsInflationPercent { get; init; } = DefaultTrayBoundsInflationPercent;
    public double TrayBoundsPaddingPixels { get; init; } = DefaultTrayBoundsPaddingPixels;
    public bool ClampTrayBoundsToLampWindow { get; init; } = DefaultClampTrayBoundsToLampWindow;
    public bool PostWarpSharpeningEnabled { get; init; } = DefaultPostWarpSharpeningEnabled;
    public double PostWarpSharpeningAmount { get; init; } = DefaultPostWarpSharpeningAmount;
    public double PostWarpSharpeningRadiusPixels { get; init; } = DefaultPostWarpSharpeningRadiusPixels;
    public int PostWarpSharpeningThreshold { get; init; } = DefaultPostWarpSharpeningThreshold;

    public FaceGenerationSettingsModel Normalize()
    {
        return new FaceGenerationSettingsModel
        {
            MaskExtractionThreshold = MaskExtractionThreshold,
            TrayBoundsInflationPercent = IsFinite(TrayBoundsInflationPercent) ? Math.Clamp(TrayBoundsInflationPercent, 0d, 1000d) : DefaultTrayBoundsInflationPercent,
            TrayBoundsPaddingPixels = IsFinite(TrayBoundsPaddingPixels) ? Math.Clamp(TrayBoundsPaddingPixels, 0d, 10000d) : DefaultTrayBoundsPaddingPixels,
            ClampTrayBoundsToLampWindow = ClampTrayBoundsToLampWindow,
            PostWarpSharpeningEnabled = PostWarpSharpeningEnabled,
            PostWarpSharpeningAmount = IsFinite(PostWarpSharpeningAmount) ? Math.Clamp(PostWarpSharpeningAmount, 0d, 2d) : DefaultPostWarpSharpeningAmount,
            PostWarpSharpeningRadiusPixels = IsFinite(PostWarpSharpeningRadiusPixels) ? Math.Clamp(PostWarpSharpeningRadiusPixels, 0.1d, 3d) : DefaultPostWarpSharpeningRadiusPixels,
            PostWarpSharpeningThreshold = Math.Clamp(PostWarpSharpeningThreshold, 0, 255)
        };
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}

public sealed class FaceRuntimeRenderAssetsModel
{
    public string? ManifestPath { get; init; }
    public string? ArtworkPath { get; init; }
    public string? MaskPath { get; init; }
    public string? TrayIdPath { get; init; }
    public string? LampIds0Path { get; init; }
    public string? LampWeights0Path { get; init; }
    public string? LampIds1Path { get; init; }
    public string? LampWeights1Path { get; init; }
    public string? TrayIdDebugPath { get; init; }
    public string? LampWeightsDebugPath { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public DateTime GeneratedUtc { get; init; }
}

public sealed class FaceMaskLayerModel
{
    public string Id { get; init; } = "face-mask-layer";
    public string Name { get; init; } = "Face Mask";
    public string? AssetPath { get; init; }
    public string? SourcePanel2DDocumentId { get; init; }
    public FaceSourceRegionModel? SourceRegion { get; init; }
    public byte ExtractionThreshold { get; init; }
    public DateTime GeneratedUtc { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public IReadOnlyList<FaceMaskContributionModel> Contributions { get; init; } = [];
}

public sealed class FaceMaskContributionModel
{
    public string? SourcePanel2DElementId { get; init; }
    public MachineObjectReference? LinkedMachineObjectReference { get; init; }
    public FaceSourceRegionModel? Bounds { get; init; }
    public int PixelCount { get; init; }
}

public sealed class FaceTrayModel
{
    public string ObjectId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsAutoAuthored { get; init; }
    public string? AutoAuthoringSource { get; init; }
    public string? SourceLampWindowObjectId { get; init; }
    public string? SourcePanel2DElementId { get; init; }
    public MachineObjectReference? LinkedMachineObjectReference { get; init; }
    public FaceSourceRegionModel? Bounds { get; init; }
    public IReadOnlyList<FacePointModel> Vertices { get; init; } = [];
    public IReadOnlyList<string> Diagnostics { get; init; } = [];
}

public sealed class FacePointModel
{
    public double X { get; init; }
    public double Y { get; init; }
}

public sealed class FaceLayerModel
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = string.Empty;
    public bool IsVisible { get; init; } = true;
    public bool IsTransformLocked { get; init; }
}

public abstract class FaceElementModel
{
    public string ObjectId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public bool IsVisible { get; init; } = true;
    public bool IsTransformLocked { get; init; }
    public MachineObjectReference? LinkedMachineObjectReference { get; init; }
    public string? LinkedPanel2DElementId { get; init; }
}

public sealed class FaceArtworkElement : FaceElementModel
{
    public string? AssetPath { get; init; }
    public string? SourcePanel2DDocumentId { get; init; }
    public FaceSourceRegionModel? SourceRegion { get; init; }
    public FaceArtworkProvenanceModel? Provenance { get; init; }
}

public sealed class FaceArtworkProvenanceModel
{
    public string Generator { get; init; } = string.Empty;
    public DateTime GeneratedAtUtc { get; init; }
    public string? SourcePanel2DElementId { get; init; }
    public string? SourcePanel2DElementKind { get; init; }
    public string? SourceAssetPath { get; init; }
    public FaceSourceRegionModel? SourceElementBounds { get; init; }
}

public sealed class FaceLampWindowElement : FaceElementModel
{
    public string? BulbMaskAssetPath { get; init; }
    public int? SourceComponentIndex { get; init; }
    public string? SharedSourceSetId { get; init; }
    public int? SharedSourceSetCount { get; init; }
    public bool SourceBlend { get; init; }
}

public sealed class FaceLampEmitterElement : FaceElementModel
{
    public string SourceLampWindowObjectId { get; init; } = string.Empty;
    public string TrayObjectId { get; init; } = string.Empty;
    public int TrayId { get; init; }
    public int? LampId { get; init; }
    public double CenterX { get; init; }
    public double CenterY { get; init; }
    public bool IsAutoAuthored { get; init; }
    public string? AutoAuthoringSource { get; init; }
    public string? EmitterPlacementSource { get; init; }
    public double? Radius { get; init; }
    public IReadOnlyList<string> Diagnostics { get; init; } = [];
}

public sealed class FaceReelDisplayElement : FaceElementModel
{
    public string? ReelSpecificationId { get; init; }
    public string? AssetPath { get; init; }
    public int? Stops { get; init; }
    public double? VisibleScale { get; init; }
    public double? BandOffset { get; init; }
    public bool IsReversed { get; init; }
    public bool ReelLampsEnabled { get; init; } = true;
    public IReadOnlyList<ReelLampSlotModel> ReelLamps { get; init; } = [];
    public bool IsOpaqueReel { get; init; }
    public string? ReelLampTransmissionMaskAssetPath { get; init; }
}

public sealed class FaceSevenSegmentDisplayElement : FaceElementModel
{
    public const int DefaultDigitCount = 1;

    public string? OnColorHex { get; init; }
    public string? OffColorHex { get; init; }
    public int DigitCount { get; init; } = DefaultDigitCount;
    public bool ShowDecimalPoint { get; init; }
}

public sealed class FaceAlphaDisplayElement : FaceElementModel
{
    public string? SegmentDisplayType { get; init; }
    public string? OnColorHex { get; init; }
    public string? OffColorHex { get; init; }
    public int DigitCount { get; init; } = 16;
    public bool ShowDecimalPoint { get; init; }
    public bool ShowCommaTail { get; init; }
    public bool IsReversed { get; init; }
}

public sealed class FaceButtonElement : FaceElementModel
{
    public MachineInputReference? LinkedInputReference { get; init; }
}
