namespace OasisEditor;

internal static class FaceDocumentCopy
{
    public static FaceDocumentModel WithGeneratedIllumination(FaceDocumentModel value,
        IReadOnlyList<FaceTrayModel> trays, IReadOnlyList<FaceLampEmitterElement> emitters) => new()
    {
        Id=value.Id, Title=value.Title, Summary=value.Summary, SourcePanel2DDocumentId=value.SourcePanel2DDocumentId,
        SourcePanel2DDocumentPath=value.SourcePanel2DDocumentPath, SourceFaceShapeId=value.SourceFaceShapeId,
        AssignedCabinetFaceTargetId=value.AssignedCabinetFaceTargetId, AssignedCabinetAssetPath=value.AssignedCabinetAssetPath,
        SourceRegion=value.SourceRegion, LastRegeneratedAtUtc=value.LastRegeneratedAtUtc, GenerationSettings=value.GenerationSettings,
        Provenance=value.Provenance, BuildState=value.BuildState, Artwork=value.Artwork, RuntimeRenderAssets=value.RuntimeRenderAssets,
        MaskLayer=value.MaskLayer, Trays=trays, LampEmitters=emitters, Layers=value.Layers, Elements=value.Elements
    };
}
