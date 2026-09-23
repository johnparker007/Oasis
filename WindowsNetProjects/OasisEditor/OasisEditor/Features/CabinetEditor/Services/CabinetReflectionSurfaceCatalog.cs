using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor.Features.CabinetEditor.Services;

public sealed record CabinetReflectionSurfaceChoice(string SourceSurfaceTargetId, string DisplayName, string AssetPath, string Label, string? CabinetTargetId, bool IsMissing = false);

public static class CabinetReflectionSurfaceCatalog
{
    public static IReadOnlyList<CabinetReflectionSurfaceChoice> FromDetectedTargets(IEnumerable<CabinetFaceTarget> detectedTargets)
    {
        ArgumentNullException.ThrowIfNull(detectedTargets);
        // GLB discovery is the target registry. Sparse authored settings never create targets.
        return detectedTargets.Where(target => target.IsValid).Select(target => target.Id).Distinct(StringComparer.Ordinal)
            .Select(target => new CabinetReflectionSurfaceChoice(target, target, string.Empty, target, target))
            .OrderBy(target => target.DisplayName, StringComparer.Ordinal)
            .ToArray();
    }
}
