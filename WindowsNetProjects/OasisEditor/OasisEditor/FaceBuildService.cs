namespace OasisEditor;

public enum FaceBuildInput
{
    ArtworkSource,
    ArtworkPreprocessing,
    ArtworkProcessing,
    LampInformation,
    MaskSettings,
    TraySettings,
    RuntimeAssetsSettings
}

public sealed record FaceBuildNodeResult(FaceGeneratedProduct Product, bool Succeeded, string? ErrorMessage = null);

public sealed class FaceBuildResult
{
    public IList<FaceGeneratedProduct> Built { get; } = new List<FaceGeneratedProduct>();
    public IList<FaceGeneratedProduct> Skipped { get; } = new List<FaceGeneratedProduct>();
    public IList<FaceBuildNodeResult> Failed { get; } = new List<FaceBuildNodeResult>();
    public bool Succeeded => Failed.Count == 0;
}

/// <summary>Face-specific dependency and state coordinator. Generation algorithms remain in their existing services.</summary>
public sealed class FaceBuildService
{
    private static readonly FaceGeneratedProduct[] s_order =
        [FaceGeneratedProduct.ArtworkCorrectionInput, FaceGeneratedProduct.BaseArtwork, FaceGeneratedProduct.ArtworkOutput, FaceGeneratedProduct.LampMask, FaceGeneratedProduct.Trays, FaceGeneratedProduct.RuntimeAssets];
    private static readonly IReadOnlyDictionary<FaceGeneratedProduct, FaceGeneratedProduct[]> s_dependencies =
        new Dictionary<FaceGeneratedProduct, FaceGeneratedProduct[]>
        {
            [FaceGeneratedProduct.ArtworkCorrectionInput] = [],
            [FaceGeneratedProduct.BaseArtwork] = [FaceGeneratedProduct.ArtworkCorrectionInput],
            [FaceGeneratedProduct.ArtworkOutput] = [FaceGeneratedProduct.BaseArtwork],
            [FaceGeneratedProduct.LampMask] = [],
            [FaceGeneratedProduct.Trays] = [FaceGeneratedProduct.LampMask],
            [FaceGeneratedProduct.RuntimeAssets] = [FaceGeneratedProduct.ArtworkOutput, FaceGeneratedProduct.LampMask, FaceGeneratedProduct.Trays]
        };

    private static readonly IReadOnlyDictionary<FaceBuildInput, FaceGeneratedProduct[]> s_invalidations =
        new Dictionary<FaceBuildInput, FaceGeneratedProduct[]>
        {
            [FaceBuildInput.ArtworkSource] = [FaceGeneratedProduct.ArtworkCorrectionInput],
            [FaceBuildInput.ArtworkPreprocessing] = [FaceGeneratedProduct.ArtworkCorrectionInput],
            [FaceBuildInput.ArtworkProcessing] = [FaceGeneratedProduct.BaseArtwork],
            [FaceBuildInput.LampInformation] = [FaceGeneratedProduct.LampMask, FaceGeneratedProduct.Trays, FaceGeneratedProduct.RuntimeAssets],
            [FaceBuildInput.MaskSettings] = [FaceGeneratedProduct.LampMask, FaceGeneratedProduct.Trays, FaceGeneratedProduct.RuntimeAssets],
            [FaceBuildInput.TraySettings] = [FaceGeneratedProduct.Trays, FaceGeneratedProduct.RuntimeAssets],
            [FaceBuildInput.RuntimeAssetsSettings] = [FaceGeneratedProduct.RuntimeAssets]
        };

    public void Invalidate(FaceBuildStateModel state, FaceBuildInput input)
    {
        ArgumentNullException.ThrowIfNull(state);
        var pending = new Queue<FaceGeneratedProduct>(s_invalidations[input]);
        var visited = new HashSet<FaceGeneratedProduct>();
        while (pending.Count > 0)
        {
            var product = pending.Dequeue();
            if (!visited.Add(product)) continue;
            var node = state.Get(product);
            if (node.Status != FaceBuildStatus.NotConfigured)
            {
                node.Status = FaceBuildStatus.Stale;
                node.ErrorMessage = null;
            }
            foreach (var dependent in s_dependencies.Where(pair => pair.Value.Contains(product)).Select(pair => pair.Key))
                pending.Enqueue(dependent);
        }
    }

    public FaceBuildResult Build(FaceBuildStateModel state,
        IReadOnlyDictionary<FaceGeneratedProduct, Func<FaceBuildNodeResult>> executors, bool force = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(executors);
        var result = new FaceBuildResult();
        var failed = new HashSet<FaceGeneratedProduct>();
        foreach (var product in s_order)
        {
            var node = state.Get(product);
            if (node.Status == FaceBuildStatus.NotConfigured || (!force && node.Status != FaceBuildStatus.Stale))
            {
                result.Skipped.Add(product);
                continue;
            }
            if (s_dependencies[product].Any(dependency => failed.Contains(dependency)
                    || state.Get(dependency).Status == FaceBuildStatus.Error))
            {
                node.Status = FaceBuildStatus.Stale;
                node.ErrorMessage = null;
                result.Skipped.Add(product);
                continue;
            }
            if (!executors.TryGetValue(product, out var execute))
            {
                node.Status = FaceBuildStatus.Error;
                node.ErrorMessage = $"No builder is available for {product}.";
                result.Failed.Add(new FaceBuildNodeResult(product, false, node.ErrorMessage));
                failed.Add(product);
                continue;
            }
            FaceBuildNodeResult build;
            try { build = execute(); }
            catch (Exception ex) { build = new FaceBuildNodeResult(product, false, ex.Message); }
            if (build.Succeeded)
            {
                node.Status = FaceBuildStatus.Current;
                node.ErrorMessage = null;
                result.Built.Add(product);
            }
            else
            {
                node.Status = FaceBuildStatus.Error;
                node.ErrorMessage = string.IsNullOrWhiteSpace(build.ErrorMessage) ? $"{product} failed to build." : build.ErrorMessage;
                result.Failed.Add(build with { ErrorMessage = node.ErrorMessage });
                failed.Add(product);
            }
        }
        return result;
    }
}

public static class FaceBuildStateFactory
{
    public static FaceProvenanceModel CreateDerivedProvenance(string? sourcePath) => new()
    {
        Artwork = Derived(sourcePath), Components = Derived(sourcePath), Illumination = Derived(sourcePath)
    };

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
            ? FaceBuildStatus.Current
            : runtimeAssetsConfigured ? FaceBuildStatus.Stale : FaceBuildStatus.NotConfigured;
        return state;
    }

    private static FaceSubsystemProvenanceModel Derived(string? sourcePath) => new()
    {
        Origin = FaceSubsystemOrigin.Derived, SourceDocumentPath = sourcePath
    };

    private static void Configure(FaceBuildStateModel state, FaceGeneratedProduct product, bool configured) =>
        state.Get(product).Status = configured ? FaceBuildStatus.Current : FaceBuildStatus.NotConfigured;
}
