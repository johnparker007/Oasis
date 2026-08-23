namespace OasisEditor;

internal static class FaceDocumentCopy
{
    public static FaceDocumentModel WithGenerationSettings(FaceDocumentModel value, FaceGenerationSettingsModel settings) => new()
    {
        Id=value.Id, Title=value.Title, Summary=value.Summary, SourcePanel2DDocumentId=value.SourcePanel2DDocumentId,
        SourcePanel2DDocumentPath=value.SourcePanel2DDocumentPath, SourceFaceShapeId=value.SourceFaceShapeId,
        AssignedCabinetFaceTargetId=value.AssignedCabinetFaceTargetId, AssignedCabinetAssetPath=value.AssignedCabinetAssetPath,
        SourceRegion=value.SourceRegion, LastRegeneratedAtUtc=value.LastRegeneratedAtUtc, GenerationSettings=settings.Normalize(),
        Provenance=value.Provenance, BuildState=value.BuildState, Artwork=value.Artwork, RuntimeRenderAssets=value.RuntimeRenderAssets,
        MaskLayer=value.MaskLayer, Trays=value.Trays, LampEmitters=value.LampEmitters, Layers=value.Layers, Elements=value.Elements
    };

    public static FaceArtworkModel WithOverride(FaceArtworkModel value, FaceArtworkOverrideModel? artworkOverride,
        int? finalWidth = null, int? finalHeight = null) => new()
    {
        Id=value.Id, Source=value.Source, Geometry=value.Geometry, ProcessingPipeline=value.ProcessingPipeline,
        CorrectionInputAssetPath=value.CorrectionInputAssetPath, BaseAssetPath=value.BaseAssetPath,
        OutputAssetPath=value.OutputAssetPath, OutputWidth=value.OutputWidth, OutputHeight=value.OutputHeight,
        Override=artworkOverride, FinalOutputWidth=finalWidth ?? value.FinalOutputWidth,
        FinalOutputHeight=finalHeight ?? value.FinalOutputHeight
    };
    public static FaceDocumentModel WithElementsAndComponents(FaceDocumentModel value,
        IReadOnlyList<FaceElementModel> elements, FaceSubsystemProvenanceModel components) => new()
    {
        Id=value.Id, Title=value.Title, Summary=value.Summary, SourcePanel2DDocumentId=value.SourcePanel2DDocumentId,
        SourcePanel2DDocumentPath=value.SourcePanel2DDocumentPath, SourceFaceShapeId=value.SourceFaceShapeId,
        AssignedCabinetFaceTargetId=value.AssignedCabinetFaceTargetId, AssignedCabinetAssetPath=value.AssignedCabinetAssetPath,
        SourceRegion=value.SourceRegion, LastRegeneratedAtUtc=value.LastRegeneratedAtUtc, GenerationSettings=value.GenerationSettings,
        Provenance=new FaceProvenanceModel { Artwork=value.Provenance.Artwork, Components=components, Illumination=value.Provenance.Illumination },
        BuildState=value.BuildState, Artwork=value.Artwork, RuntimeRenderAssets=value.RuntimeRenderAssets, MaskLayer=value.MaskLayer,
        Trays=value.Trays, LampEmitters=value.LampEmitters, Layers=value.Layers, Elements=elements
    };

    public static FaceSubsystemProvenanceModel MarkComponentsModified(FaceSubsystemProvenanceModel value) =>
        value.Origin == FaceSubsystemOrigin.Derived && !value.IsLocallyModified
            ? new FaceSubsystemProvenanceModel { Origin=value.Origin, SourceDocumentPath=value.SourceDocumentPath, IsLocallyModified=true }
            : value;
    public static FaceSubsystemProvenanceModel MarkIlluminationModified(FaceSubsystemProvenanceModel value) =>
        value.Origin == FaceSubsystemOrigin.Derived && !value.IsLocallyModified
            ? new FaceSubsystemProvenanceModel { Origin=value.Origin, SourceDocumentPath=value.SourceDocumentPath, IsLocallyModified=true }
            : value;

    public static FaceDocumentModel WithIllumination(FaceDocumentModel value, IReadOnlyList<FaceElementModel> elements,
        FaceMaskLayerModel? maskLayer, IReadOnlyList<FaceTrayModel> trays, IReadOnlyList<FaceLampEmitterElement> emitters,
        FaceSubsystemProvenanceModel provenance) => new()
    {
        Id=value.Id, Title=value.Title, Summary=value.Summary, SourcePanel2DDocumentId=value.SourcePanel2DDocumentId,
        SourcePanel2DDocumentPath=value.SourcePanel2DDocumentPath, SourceFaceShapeId=value.SourceFaceShapeId,
        AssignedCabinetFaceTargetId=value.AssignedCabinetFaceTargetId, AssignedCabinetAssetPath=value.AssignedCabinetAssetPath,
        SourceRegion=value.SourceRegion, LastRegeneratedAtUtc=value.LastRegeneratedAtUtc, GenerationSettings=value.GenerationSettings,
        Provenance=new FaceProvenanceModel { Artwork=value.Provenance.Artwork, Components=value.Provenance.Components, Illumination=provenance },
        BuildState=value.BuildState, Artwork=value.Artwork, RuntimeRenderAssets=value.RuntimeRenderAssets, MaskLayer=maskLayer,
        Trays=trays, LampEmitters=emitters, Layers=value.Layers, Elements=elements
    };

    public static FaceDocumentModel WithMaskLayer(FaceDocumentModel value, FaceMaskLayerModel maskLayer) =>
        Copy(value, maskLayer, value.Trays, value.LampEmitters);

    public static FaceDocumentModel WithArtwork(FaceDocumentModel value, FaceArtworkModel artwork, FaceSubsystemProvenanceModel artworkProvenance) => new()
    {
        Id=value.Id, Title=value.Title, Summary=value.Summary, SourcePanel2DDocumentId=value.SourcePanel2DDocumentId,
        SourcePanel2DDocumentPath=value.SourcePanel2DDocumentPath, SourceFaceShapeId=value.SourceFaceShapeId,
        AssignedCabinetFaceTargetId=value.AssignedCabinetFaceTargetId, AssignedCabinetAssetPath=value.AssignedCabinetAssetPath,
        SourceRegion=value.SourceRegion, LastRegeneratedAtUtc=value.LastRegeneratedAtUtc, GenerationSettings=value.GenerationSettings,
        Provenance=new FaceProvenanceModel { Artwork=artworkProvenance, Components=value.Provenance.Components, Illumination=value.Provenance.Illumination },
        BuildState=value.BuildState, Artwork=artwork, RuntimeRenderAssets=value.RuntimeRenderAssets, MaskLayer=value.MaskLayer,
        Trays=value.Trays, LampEmitters=value.LampEmitters, Layers=value.Layers, Elements=value.Elements
    };

    public static FaceDocumentModel WithArtworkAndVisual(FaceDocumentModel value, FaceArtworkModel artwork,
        FaceSubsystemProvenanceModel artworkProvenance)
    {
        var existing = value.Elements.OfType<FaceArtworkElement>().FirstOrDefault();
        var logicalWidth = value.SourceRegion?.Width is > 0 ? value.SourceRegion.Width : FaceDocumentStorage.DefaultNativeLogicalWidth;
        var logicalHeight = value.SourceRegion?.Height is > 0 ? value.SourceRegion.Height : FaceDocumentStorage.DefaultNativeLogicalHeight;
        var visual = new FaceArtworkElement
        {
            ObjectId = existing?.ObjectId ?? $"face-artwork-{Guid.NewGuid():N}", Name = existing?.Name ?? "Face artwork",
            X = existing?.X ?? 0, Y = existing?.Y ?? 0, Width = existing?.Width ?? logicalWidth,
            Height = existing?.Height ?? logicalHeight, IsVisible = existing?.IsVisible ?? true,
            IsTransformLocked = true, AssetPath = artwork.OutputAssetPath
        };
        var elements = value.Elements.Where(element => element is not FaceArtworkElement).Prepend(visual).ToArray();
        var copy = WithArtwork(value, artwork, artworkProvenance);
        return new FaceDocumentModel
        {
            Id=copy.Id, Title=copy.Title, Summary=copy.Summary, SourcePanel2DDocumentId=copy.SourcePanel2DDocumentId,
            SourcePanel2DDocumentPath=copy.SourcePanel2DDocumentPath, SourceFaceShapeId=copy.SourceFaceShapeId,
            AssignedCabinetFaceTargetId=copy.AssignedCabinetFaceTargetId, AssignedCabinetAssetPath=copy.AssignedCabinetAssetPath,
            SourceRegion=copy.SourceRegion, LastRegeneratedAtUtc=copy.LastRegeneratedAtUtc, GenerationSettings=copy.GenerationSettings,
            Provenance=copy.Provenance, BuildState=copy.BuildState, Artwork=copy.Artwork, RuntimeRenderAssets=copy.RuntimeRenderAssets,
            MaskLayer=copy.MaskLayer, Trays=copy.Trays, LampEmitters=copy.LampEmitters, Layers=copy.Layers, Elements=elements
        };
    }

    public static FaceDocumentModel WithGeneratedIllumination(FaceDocumentModel value,
        IReadOnlyList<FaceTrayModel> trays, IReadOnlyList<FaceLampEmitterElement> emitters) =>
        Copy(value, value.MaskLayer, trays, emitters);

    private static FaceDocumentModel Copy(FaceDocumentModel value, FaceMaskLayerModel? maskLayer,
        IReadOnlyList<FaceTrayModel> trays, IReadOnlyList<FaceLampEmitterElement> emitters) => new()
    {
        Id=value.Id, Title=value.Title, Summary=value.Summary, SourcePanel2DDocumentId=value.SourcePanel2DDocumentId,
        SourcePanel2DDocumentPath=value.SourcePanel2DDocumentPath, SourceFaceShapeId=value.SourceFaceShapeId,
        AssignedCabinetFaceTargetId=value.AssignedCabinetFaceTargetId, AssignedCabinetAssetPath=value.AssignedCabinetAssetPath,
        SourceRegion=value.SourceRegion, LastRegeneratedAtUtc=value.LastRegeneratedAtUtc, GenerationSettings=value.GenerationSettings,
        Provenance=value.Provenance, BuildState=value.BuildState, Artwork=value.Artwork, RuntimeRenderAssets=value.RuntimeRenderAssets,
        MaskLayer=maskLayer, Trays=trays, LampEmitters=emitters, Layers=value.Layers, Elements=value.Elements
    };
}
