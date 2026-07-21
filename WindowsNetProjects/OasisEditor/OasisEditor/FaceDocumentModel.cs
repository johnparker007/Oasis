namespace OasisEditor;

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
    public FaceRuntimeRenderAssetsModel? RuntimeRenderAssets { get; init; }
    public FaceMaskLayerModel? MaskLayer { get; init; }
    public IReadOnlyList<FaceTrayModel> Trays { get; init; } = [];
    public IReadOnlyList<FaceLampEmitterElement> LampEmitters { get; init; } = [];
    public IReadOnlyList<FaceLayerModel> Layers { get; init; } = [];
    public IReadOnlyList<FaceElementModel> Elements { get; init; } = [];
}


public sealed class FaceGenerationSettingsModel
{
    public const byte DefaultMaskExtractionThreshold = 1;
    public const double DefaultTrayBoundsInflationPercent = 0d;
    public const double DefaultTrayBoundsPaddingPixels = 0d;
    public const bool DefaultClampTrayBoundsToLampWindow = false;

    public static FaceGenerationSettingsModel Default { get; } = new();

    public byte MaskExtractionThreshold { get; init; } = DefaultMaskExtractionThreshold;
    public double TrayBoundsInflationPercent { get; init; } = DefaultTrayBoundsInflationPercent;
    public double TrayBoundsPaddingPixels { get; init; } = DefaultTrayBoundsPaddingPixels;
    public bool ClampTrayBoundsToLampWindow { get; init; } = DefaultClampTrayBoundsToLampWindow;

    public FaceGenerationSettingsModel Normalize()
    {
        return new FaceGenerationSettingsModel
        {
            MaskExtractionThreshold = MaskExtractionThreshold,
            TrayBoundsInflationPercent = IsFinite(TrayBoundsInflationPercent) ? Math.Clamp(TrayBoundsInflationPercent, 0d, 1000d) : DefaultTrayBoundsInflationPercent,
            TrayBoundsPaddingPixels = IsFinite(TrayBoundsPaddingPixels) ? Math.Clamp(TrayBoundsPaddingPixels, 0d, 10000d) : DefaultTrayBoundsPaddingPixels,
            ClampTrayBoundsToLampWindow = ClampTrayBoundsToLampWindow
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
}

public sealed class FaceSevenSegmentDisplayElement : FaceElementModel
{
    public string? OnColorHex { get; init; }
    public string? OffColorHex { get; init; }
    public bool ShowDecimalPoint { get; init; }
}

public sealed class FaceAlphaDisplayElement : FaceElementModel
{
    public string? SegmentDisplayType { get; init; }
    public string? OnColorHex { get; init; }
    public string? OffColorHex { get; init; }
    public bool ShowDecimalPoint { get; init; }
    public bool ShowCommaTail { get; init; }
    public bool IsReversed { get; init; }
}

public sealed class FaceButtonElement : FaceElementModel
{
    public MachineInputReference? LinkedInputReference { get; init; }
}
