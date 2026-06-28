using System.IO;
using HelixToolkit.Wpf;

namespace OasisEditor.Features.CabinetEditor.Services;

public sealed class HelixCabinetModelLoader : ICabinetModelLoader
{
    private readonly ICabinetModelLoader _fallbackLoader = new SharpGltfWpfModelLoader();
    private readonly ICabinetFaceTargetDetector _faceTargetDetector = new GlbCabinetFaceTargetDetector();

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
                    return LoadWithFallback(modelPath, cancellationToken, "Helix loaded the .glb, but it did not contain displayable geometry.");
                }

                var faceTargets = _faceTargetDetector.DetectTargets(modelPath, cancellationToken);
                return CabinetModelLoadResult.Success(model, faceTargets);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return LoadWithFallback(modelPath, cancellationToken, $"Helix could not load the .glb: {ex.Message}");
            }
        }, cancellationToken);
    }

    private CabinetModelLoadResult LoadWithFallback(string modelPath, CancellationToken cancellationToken, string helixFailureMessage)
    {
        var fallbackResult = _fallbackLoader.LoadAsync(modelPath, cancellationToken).GetAwaiter().GetResult();
        if (fallbackResult.Succeeded)
        {
            return fallbackResult;
        }

        var fallbackError = string.IsNullOrWhiteSpace(fallbackResult.ErrorMessage)
            ? "SharpGLTF fallback also failed."
            : fallbackResult.ErrorMessage;
        return CabinetModelLoadResult.Failure($"Unable to load cabinet model. {helixFailureMessage} {fallbackError}");
    }
}
