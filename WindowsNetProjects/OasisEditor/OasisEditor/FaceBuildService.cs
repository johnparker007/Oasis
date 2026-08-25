using System.IO;
using OasisEditor.Progress;

namespace OasisEditor;

public enum FaceBuildInput
{
    ArtworkSource,
    ArtworkPreprocessing,
    ArtworkProcessing,
    ArtworkOverride,
    Components,
    LampInformation,
    LampMaskSource,
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
            [FaceBuildInput.ArtworkOverride] = [FaceGeneratedProduct.ArtworkOutput],
            [FaceBuildInput.Components] = [FaceGeneratedProduct.RuntimeAssets],
            [FaceBuildInput.LampInformation] = [FaceGeneratedProduct.Trays, FaceGeneratedProduct.RuntimeAssets],
            [FaceBuildInput.LampMaskSource] = [FaceGeneratedProduct.LampMask, FaceGeneratedProduct.Trays, FaceGeneratedProduct.RuntimeAssets],
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
        IReadOnlyDictionary<FaceGeneratedProduct, Func<FaceBuildNodeResult>> executors, bool force = false,
        IReadOnlySet<FaceGeneratedProduct>? includedProducts = null,
        IEditorProgressReporter? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(executors);
        progress ??= NoOpEditorProgressReporter.Instance;
        var result = new FaceBuildResult();
        var failed = new HashSet<FaceGeneratedProduct>();
        var productsToBuild = s_order.Where(product =>
            (includedProducts is null || includedProducts.Contains(product))
            && state.Get(product).Status != FaceBuildStatus.NotConfigured
            && (force || state.Get(product).Status == FaceBuildStatus.Stale)).ToArray();
        var completedProducts = 0;
        progress.Report(0d, productsToBuild.Length == 0 ? "Face outputs are already current." : $"Preparing {productsToBuild.Length} Face output{(productsToBuild.Length == 1 ? "" : "s")}...");
        foreach (var product in s_order)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (includedProducts is not null && !includedProducts.Contains(product))
            {
                result.Skipped.Add(product);
                continue;
            }
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
                completedProducts++;
                progress.Report((double)completedProducts / productsToBuild.Length, $"{DisplayName(product)} failed.");
                continue;
            }
            FaceBuildNodeResult build;
            progress.Report((double)completedProducts / productsToBuild.Length, $"Building {DisplayName(product)}...");
            try { build = execute(); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { build = new FaceBuildNodeResult(product, false, ex.Message); }
            cancellationToken.ThrowIfCancellationRequested();
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
            completedProducts++;
            progress.Report((double)completedProducts / productsToBuild.Length,
                build.Succeeded ? $"Built {DisplayName(product)}." : $"{DisplayName(product)} failed.");
            cancellationToken.ThrowIfCancellationRequested();
        }
        progress.Report(1d, result.Succeeded ? "Face build complete." : "Face build completed with errors.");
        return result;
    }

    private static string DisplayName(FaceGeneratedProduct product) => product switch
    {
        FaceGeneratedProduct.ArtworkCorrectionInput => "artwork correction input",
        FaceGeneratedProduct.BaseArtwork => "processed artwork",
        FaceGeneratedProduct.ArtworkOutput => "artwork output",
        FaceGeneratedProduct.LampMask => "lamp mask",
        FaceGeneratedProduct.Trays => "trays and illumination",
        FaceGeneratedProduct.RuntimeAssets => "runtime assets",
        _ => product.ToString()
    };
}

/// <summary>Reconciles recipe availability with generated-product configuration before freshness invalidation/build.</summary>
public static class FaceBuildConfigurationService
{
    private static readonly FaceGeneratedProduct[] s_artworkProducts =
        [FaceGeneratedProduct.ArtworkCorrectionInput, FaceGeneratedProduct.BaseArtwork, FaceGeneratedProduct.ArtworkOutput];

    public static void ReconcileArtwork(FaceDocumentModel face) => ReconcileArtwork(face.Artwork, face.BuildState);

    public static void ReconcileArtwork(FaceArtworkModel? artwork, FaceBuildStateModel state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var configured = IsArtworkRecipeConfigured(artwork);
        foreach (var product in s_artworkProducts)
        {
            var node = state.Get(product);
            if (!configured)
            {
                node.Status = FaceBuildStatus.NotConfigured;
                node.ErrorMessage = null;
            }
            else if (node.Status == FaceBuildStatus.NotConfigured)
            {
                node.Status = FaceBuildStatus.Stale;
                node.ErrorMessage = null;
            }
        }
    }

    public static bool IsArtworkRecipeConfigured(FaceArtworkModel? artwork)
    {
        if (artwork is null || string.IsNullOrWhiteSpace(artwork.Source.AssetPath)
            || string.IsNullOrWhiteSpace(artwork.CorrectionInputAssetPath)
            || string.IsNullOrWhiteSpace(artwork.BaseAssetPath)
            || string.IsNullOrWhiteSpace(artwork.OutputAssetPath)
            || artwork.OutputWidth <= 0 || artwork.OutputHeight <= 0)
            return false;

        if (artwork.Source.Kind == FaceArtworkSourceKind.Image)
            return !Path.IsPathRooted(artwork.Source.AssetPath)
                && artwork.Source.PixelWidth > 0 && artwork.Source.PixelHeight > 0
                && artwork.Geometry.PerspectiveRegistration.IsValid();

        return !string.IsNullOrWhiteSpace(artwork.Source.Panel2DDocumentId)
            && !string.IsNullOrWhiteSpace(artwork.Source.FaceSourceShapeId);
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
