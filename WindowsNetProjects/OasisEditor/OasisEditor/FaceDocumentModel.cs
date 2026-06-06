namespace OasisEditor;

public sealed class FaceDocumentModel
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
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

public sealed class FaceLampWindowElement : FaceElementModel
{
}
