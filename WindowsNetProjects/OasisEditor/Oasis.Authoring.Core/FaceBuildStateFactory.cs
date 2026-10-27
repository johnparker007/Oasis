using OasisEditor;

namespace OasisEditor.FacePipeline;

public static class FaceBuildStateFactory
{
    public static FaceProvenanceModel CreateDerivedProvenance(string? sourcePath) => new()
    { Artwork = Derived(sourcePath), Components = Derived(sourcePath), Illumination = Derived(sourcePath) };

    public static FaceBuildStateModel CreateGeneratedState(bool artwork, bool mask, bool trays,
        bool runtimeAssetsCurrent, bool runtimeAssetsConfigured)
    {
        var state = new FaceBuildStateModel();
        Configure(state, FaceGeneratedProduct.ArtworkCorrectionInput, artwork);
        Configure(state, FaceGeneratedProduct.BaseArtwork, artwork);
        Configure(state, FaceGeneratedProduct.ArtworkOutput, artwork);
        Configure(state, FaceGeneratedProduct.LampMask, mask);
        Configure(state, FaceGeneratedProduct.Trays, trays);
        state.Get(FaceGeneratedProduct.RuntimeAssets).Status = runtimeAssetsCurrent
            ? FaceBuildStatus.Current : runtimeAssetsConfigured ? FaceBuildStatus.Stale : FaceBuildStatus.NotConfigured;
        return state;
    }

    private static FaceSubsystemProvenanceModel Derived(string? sourcePath) => new()
    { Origin = FaceSubsystemOrigin.Derived, SourceDocumentPath = sourcePath };
    private static void Configure(FaceBuildStateModel state, FaceGeneratedProduct product, bool configured) =>
        state.Get(product).Status = configured ? FaceBuildStatus.Current : FaceBuildStatus.NotConfigured;
}
