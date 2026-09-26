using System.IO;
using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor.Features.CabinetEditor.Services;

public sealed record CabinetFaceTargetDiscoveryResult(bool Succeeded, IReadOnlyList<CabinetFaceTarget> Targets)
{
    public static CabinetFaceTargetDiscoveryResult Unavailable { get; } = new(false, []);
}

/// <summary>Shared ordered Cabinet geometry-target discovery used by Machine Details and Overview.</summary>
public static class CabinetFaceTargetDiscovery
{
    public static CabinetFaceTargetDiscoveryResult Discover(string cabinetManifestPath, CabinetDocument cabinet,
        ICabinetFaceTargetDetector detector)
    {
        if (string.IsNullOrWhiteSpace(cabinetManifestPath) || string.IsNullOrWhiteSpace(cabinet.Model.Path))
            return CabinetFaceTargetDiscoveryResult.Unavailable;
        try
        {
            var modelPath = Path.IsPathFullyQualified(cabinet.Model.Path)
                ? cabinet.Model.Path
                : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(cabinetManifestPath)!, cabinet.Model.Path));
            if (!File.Exists(modelPath)) return CabinetFaceTargetDiscoveryResult.Unavailable;
            var detected = detector.DetectTargets(modelPath, CancellationToken.None);
            var targets = detected.Where(target => target.IsValid).ToArray();
            if (detected.Count > 0 && targets.Length == 0) return CabinetFaceTargetDiscoveryResult.Unavailable;
            return new(true, targets);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return CabinetFaceTargetDiscoveryResult.Unavailable;
        }
    }
}
