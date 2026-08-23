namespace OasisEditor;

internal static class FaceDocumentCopy
{
    public static FaceDocumentModel WithMaskLayer(FaceDocumentModel value, FaceMaskLayerModel maskLayer) =>
        Copy(value, maskLayer, value.Trays, value.LampEmitters);

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
