using System.IO;
using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor.Features.CabinetEditor.Services;

public sealed record CabinetReflectionFaceChoice(string FaceId, string DisplayName, string AssetPath, string Label, string? CabinetTargetId, bool IsMissing = false);

public static class CabinetReflectionFaceCatalog
{
    public static IReadOnlyList<CabinetReflectionFaceChoice> Discover(string cabinetManifestPath)
    {
        var cabinetPackage = Path.GetDirectoryName(cabinetManifestPath);
        var assets = Directory.GetParent(Directory.GetParent(cabinetPackage ?? string.Empty)?.FullName ?? string.Empty)?.FullName;
        var faceRoot = assets is null ? null : Path.Combine(assets, "Faces");
        if (faceRoot is null || !Directory.Exists(faceRoot)) return [];
        var raw = new List<CabinetReflectionFaceChoice>();
        foreach (var path in Directory.EnumerateFiles(faceRoot, ProjectAssetPathService.FaceManifestFileName, SearchOption.AllDirectories))
        {
            if (!FaceDocumentStorage.TryReadValidated(File.ReadAllText(path), out var file, out _)) continue;
            var face = FaceDocumentStorage.ToModel(file);
            if (string.IsNullOrWhiteSpace(face.Id)) continue;
            var name = ProjectAssetPathService.GetPackageAssetNameFromManifestPath(path, EditorAssetType.Face) ?? Path.GetFileName(Path.GetDirectoryName(path));
            var relative = assets is null ? path : ProjectAssetPathService.NormalizeProjectRelativePath(Path.GetRelativePath(Directory.GetParent(assets)!.FullName, Path.GetDirectoryName(path)!));
            raw.Add(new(face.Id.Trim(), name, relative, name, face.AssignedCabinetFaceTargetId));
        }
        var duplicates = raw.GroupBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return raw.Select(item => item with { Label = duplicates.Contains(item.DisplayName) ? $"{item.DisplayName} — {item.AssetPath}" : item.DisplayName })
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.AssetPath, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
