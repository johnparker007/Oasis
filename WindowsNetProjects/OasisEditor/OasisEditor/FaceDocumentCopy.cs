namespace OasisEditor;

internal static class FaceDocumentCopy
{
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
