namespace OasisEditor;

public enum FaceBuildInput
{
    ArtworkSource,
    ArtworkCorrection,
    LampInformation,
    MaskSettings,
    TraySettings,
    RuntimeLightingSettings
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
        [FaceGeneratedProduct.ArtworkOutput, FaceGeneratedProduct.LampMask, FaceGeneratedProduct.Trays, FaceGeneratedProduct.RuntimeLighting];
    private static readonly IReadOnlyDictionary<FaceGeneratedProduct, FaceGeneratedProduct[]> s_dependencies =
        new Dictionary<FaceGeneratedProduct, FaceGeneratedProduct[]>
        {
            [FaceGeneratedProduct.ArtworkOutput] = [],
            [FaceGeneratedProduct.LampMask] = [],
            [FaceGeneratedProduct.Trays] = [FaceGeneratedProduct.LampMask],
            [FaceGeneratedProduct.RuntimeLighting] = [FaceGeneratedProduct.ArtworkOutput, FaceGeneratedProduct.LampMask, FaceGeneratedProduct.Trays]
        };

    private static readonly IReadOnlyDictionary<FaceBuildInput, FaceGeneratedProduct[]> s_invalidations =
        new Dictionary<FaceBuildInput, FaceGeneratedProduct[]>
        {
            [FaceBuildInput.ArtworkSource] = [FaceGeneratedProduct.ArtworkOutput, FaceGeneratedProduct.RuntimeLighting],
            [FaceBuildInput.ArtworkCorrection] = [FaceGeneratedProduct.ArtworkOutput, FaceGeneratedProduct.RuntimeLighting],
            [FaceBuildInput.LampInformation] = [FaceGeneratedProduct.LampMask, FaceGeneratedProduct.Trays, FaceGeneratedProduct.RuntimeLighting],
            [FaceBuildInput.MaskSettings] = [FaceGeneratedProduct.LampMask, FaceGeneratedProduct.Trays, FaceGeneratedProduct.RuntimeLighting],
            [FaceBuildInput.TraySettings] = [FaceGeneratedProduct.Trays, FaceGeneratedProduct.RuntimeLighting],
            [FaceBuildInput.RuntimeLightingSettings] = [FaceGeneratedProduct.RuntimeLighting]
        };

    public void Invalidate(FaceBuildStateModel state, FaceBuildInput input)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (var product in s_invalidations[input])
        {
            var node = state.Get(product);
            if (node.Status == FaceBuildStatus.NotConfigured) continue;
            node.Status = FaceBuildStatus.Stale;
            node.ErrorMessage = null;
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
        bool runtimeLighting, bool runtimeLightingConfigured)
    {
        var state = new FaceBuildStateModel();
        Configure(state, FaceGeneratedProduct.ArtworkOutput, artwork);
        Configure(state, FaceGeneratedProduct.LampMask, mask);
        Configure(state, FaceGeneratedProduct.Trays, trays);
        state.Get(FaceGeneratedProduct.RuntimeLighting).Status = runtimeLighting
            ? FaceBuildStatus.Current
            : runtimeLightingConfigured ? FaceBuildStatus.Stale : FaceBuildStatus.NotConfigured;
        return state;
    }

    private static FaceSubsystemProvenanceModel Derived(string? sourcePath) => new()
    {
        Origin = FaceSubsystemOrigin.Derived, SourceDocumentPath = sourcePath
    };

    private static void Configure(FaceBuildStateModel state, FaceGeneratedProduct product, bool configured) =>
        state.Get(product).Status = configured ? FaceBuildStatus.Current : FaceBuildStatus.NotConfigured;
}
