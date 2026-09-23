using System.IO;
using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor.Features.CabinetEditor.Services;

public sealed record CabinetReflectionSurfaceChoice(string SourceSurfaceTargetId, string DisplayName, string AssetPath, string Label, string? CabinetTargetId, bool IsMissing = false);

public static class CabinetReflectionSurfaceCatalog
{
    public static IReadOnlyList<CabinetReflectionSurfaceChoice> Discover(string? assetsDirectory, CabinetDocument? cabinet = null)
    {
        // Reflection sources are reusable Cabinet surface targets, never installed Face assets.
        var targetIds = (cabinet?.TargetOverrides ?? []).Select(target => target.TargetId).Where(target => !string.IsNullOrWhiteSpace(target)).ToList();
        if (cabinet is not null && Path.IsPathFullyQualified(cabinet.Model.Path) && File.Exists(cabinet.Model.Path))
            targetIds.AddRange(new GlbCabinetFaceTargetDetector().DetectTargets(cabinet.Model.Path, CancellationToken.None).Where(target => target.IsValid).Select(target => target.Id));
        return targetIds.Distinct(StringComparer.Ordinal)
            .Select(target => new CabinetReflectionSurfaceChoice(target, target, string.Empty, target, target))
            .OrderBy(target => target.DisplayName, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsExpectedDiscoveryFailure(Exception exception) => exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException;
}
