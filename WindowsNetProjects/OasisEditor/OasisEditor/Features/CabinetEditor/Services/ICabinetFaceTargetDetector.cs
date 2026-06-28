using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor.Features.CabinetEditor.Services;

public interface ICabinetFaceTargetDetector
{
    IReadOnlyList<CabinetFaceTarget> DetectTargets(string modelPath, CancellationToken cancellationToken = default);
}
