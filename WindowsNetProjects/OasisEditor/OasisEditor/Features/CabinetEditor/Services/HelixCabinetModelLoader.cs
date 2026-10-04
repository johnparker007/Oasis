namespace OasisEditor.Features.CabinetEditor.Services;

public sealed class HelixCabinetModelLoader : ICabinetModelLoader
{
    private readonly ICabinetModelLoader _fallbackLoader = new SharpGltfWpfModelLoader();

    public Task<CabinetModelLoadResult> LoadAsync(string modelPath, CancellationToken cancellationToken = default)
        => _fallbackLoader.LoadAsync(modelPath, cancellationToken);
}
