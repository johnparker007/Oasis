using System.IO;
using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor;

public sealed class OasisAssetLibraryCatalog
{
    public IReadOnlyList<LibraryAssetCatalogEntry> Discover(string? libraryRoot)
    {
        if (string.IsNullOrWhiteSpace(libraryRoot) || !Directory.Exists(libraryRoot)) return [];
        var entries = new List<LibraryAssetCatalogEntry>();
        DiscoverType(libraryRoot, "Cabinets", ProjectAssetPathService.Cabinet3DManifestFileName, EditorAssetType.Cabinet3D, entries);
        DiscoverType(libraryRoot, "Reels", ProjectAssetPathService.ReelManifestFileName, EditorAssetType.Reel, entries);
        DiscoverType(libraryRoot, "Object3D", ProjectAssetPathService.Object3DManifestFileName, EditorAssetType.Object3D, entries);
        return entries.OrderBy(entry => entry.AssetType).ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(entry => entry.Reference.Path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void DiscoverType(string root, string folder, string manifestName, EditorAssetType type, List<LibraryAssetCatalogEntry> entries)
    {
        var typeRoot = Path.Combine(root, folder);
        if (!Directory.Exists(typeRoot)) return;
        foreach (var manifest in Directory.EnumerateFiles(typeRoot, manifestName, SearchOption.AllDirectories))
        {
            try
            {
                var json = File.ReadAllText(manifest);
                var valid = type switch
                {
                    EditorAssetType.Cabinet3D => CabinetDocumentStorage.TryRead(json, out _),
                    EditorAssetType.Reel => ReelDocumentStorage.TryRead(json, out var reel, out _) && !string.IsNullOrWhiteSpace(reel.Id),
                    EditorAssetType.Object3D => Object3DDocumentStorage.TryRead(json, out var object3D, out _) && TryValidateObject3DPackage(object3D, manifest),
                    _ => false
                };
                if (!valid) continue;
                var displayName = type switch
                {
                    EditorAssetType.Reel when ReelDocumentStorage.TryRead(json, out var reelDocument, out _) => reelDocument.DisplayName,
                    EditorAssetType.Object3D when Object3DDocumentStorage.TryRead(json, out var objectDocument, out _) => objectDocument.DisplayName,
                    _ => Path.GetFileName(Path.GetDirectoryName(manifest))
                };
                var relative = AssetReference.Normalize(Path.GetRelativePath(root, manifest));
                entries.Add(new(type, displayName, AssetReference.Library(relative), manifest));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException) { }
        }
    }

    private static bool TryValidateObject3DPackage(Object3DDocument document, string manifest)
    {
        try { Object3DValidationService.Validate(document, Path.GetDirectoryName(manifest)); return true; }
        catch (InvalidOperationException) { return false; }
    }
}
