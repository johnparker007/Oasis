using System.IO;
using HelixToolkit.Wpf;

namespace OasisEditor.Features.CabinetEditor.Services;

public sealed class HelixCabinetModelLoader : ICabinetModelLoader
{
    public Task<CabinetModelLoadResult> LoadAsync(string modelPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            return Task.FromResult(CabinetModelLoadResult.Failure("Choose a .glb cabinet model to load."));
        }

        if (!File.Exists(modelPath))
        {
            return Task.FromResult(CabinetModelLoadResult.Failure($"Cabinet model file was not found: {modelPath}"));
        }

        if (!string.Equals(Path.GetExtension(modelPath), ".glb", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(CabinetModelLoadResult.Failure("Cabinet Model Viewer currently supports .glb files only."));
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var importer = new ModelImporter();
                var model = importer.Load(modelPath);
                if (model.Children.Count == 0)
                {
                    return CabinetModelLoadResult.Failure("The .glb loaded, but it did not contain displayable geometry.");
                }

                return CabinetModelLoadResult.Success(model);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return CabinetModelLoadResult.Failure($"Unable to load cabinet model: {ex.Message}");
            }
        }, cancellationToken);
    }
}
