namespace OasisEditor.Features.CabinetEditor.Services;

public interface ICabinetModelLoader
{
    Task<CabinetModelLoadResult> LoadAsync(string modelPath, CancellationToken cancellationToken = default);
}
