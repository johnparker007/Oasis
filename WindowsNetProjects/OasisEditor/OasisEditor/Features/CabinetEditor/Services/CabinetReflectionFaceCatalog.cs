using System.IO;
using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor.Features.CabinetEditor.Services;

public sealed record CabinetReflectionFaceChoice(string FaceId, string DisplayName, string AssetPath, string Label, string? CabinetTargetId, bool IsMissing = false);

public static class CabinetReflectionFaceCatalog
{
    public static IReadOnlyList<CabinetReflectionFaceChoice> Discover(string? assetsDirectory)
    {
        if (string.IsNullOrWhiteSpace(assetsDirectory)) return [];
        try
        {
            var assets = Path.GetFullPath(assetsDirectory);
            var faceRoot = Path.Combine(assets, "Faces");
            if (!Directory.Exists(faceRoot)) return [];
            var raw = new List<CabinetReflectionFaceChoice>();
            foreach (var path in Directory.EnumerateFiles(faceRoot, ProjectAssetPathService.FaceManifestFileName, SearchOption.AllDirectories))
            {
                try
                {
                    if (!FaceDocumentStorage.TryReadValidated(File.ReadAllText(path), out var file, out _)) continue;
                    var face = FaceDocumentStorage.ToModel(file);
                    if (string.IsNullOrWhiteSpace(face.Id)) continue;
                    var name = ProjectAssetPathService.GetPackageAssetNameFromManifestPath(path, EditorAssetType.Face) ?? Path.GetFileName(Path.GetDirectoryName(path));
                    var relative = ProjectAssetPathService.NormalizeProjectRelativePath(Path.Combine(Path.GetFileName(assets), Path.GetRelativePath(assets, Path.GetDirectoryName(path)!)));
                    raw.Add(new(face.Id.Trim(), name, relative, name, face.AssignedCabinetFaceTargetId));
                }
                catch (Exception exception) when (IsExpectedDiscoveryFailure(exception)) { }
            }
            var duplicates = raw.GroupBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return raw.Select(item => item with { Label = duplicates.Contains(item.DisplayName) ? $"{item.DisplayName} — {item.AssetPath}" : item.DisplayName })
                .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.AssetPath, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        catch (Exception exception) when (IsExpectedDiscoveryFailure(exception)) { return []; }
    }

    private static bool IsExpectedDiscoveryFailure(Exception exception) => exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException;
}
