namespace OasisEditor;

public sealed class FaceDocumentModel
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string? SourcePanel2DDocumentId { get; init; }
    public FaceSourceRegionModel? SourceRegion { get; init; }
    public IReadOnlyList<FaceLayerModel> Layers { get; init; } = [];
    public IReadOnlyList<FaceElementModel> Elements { get; init; } = [];
}

public sealed class FaceLayerModel
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = string.Empty;
    public bool IsVisible { get; init; } = true;
    public bool IsLocked { get; init; }
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
    public bool IsLocked { get; init; }
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
}

public sealed class FaceSevenSegmentDisplayElement : FaceElementModel
{
    public string? OnColorHex { get; init; }
    public string? OffColorHex { get; init; }
    public bool ShowDecimalPoint { get; init; }
}

public sealed class FaceButtonElement : FaceElementModel
{
    public MachineInputReference? LinkedInputReference { get; init; }
}
