using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace OasisEditor;

public static class AssetInspectorDetailsBuilder
{
    private static readonly string[] KnownManifestFiles =
    [
        ProjectAssetPathService.Panel2DManifestFileName,
        ProjectAssetPathService.FaceManifestFileName,
        ProjectAssetPathService.Cabinet3DManifestFileName,
        "asset.machine"
    ];

    public static void BuildRows(ICollection<InspectorPropertyRowViewModel> rows, EditorProject project, string path, bool isDirectory)
    {
        if (isDirectory)
        {
            BuildFolderRows(rows, project, path);
            return;
        }

        if (!File.Exists(path))
        {
            Add(rows, "Warning", "Status", "File does not exist.");
            return;
        }

        if (TryGetManifestType(path, out var assetType))
        {
            BuildManifestRows(rows, project, path, assetType);
            return;
        }

        if (IsSupportedImage(path))
        {
            BuildImageRows(rows, project, path);
            return;
        }

        BuildGenericFileRows(rows, project, path);
    }

    public static string GetTitle(EditorProject project, string path, bool isDirectory)
    {
        if (isDirectory)
        {
            return $"Folder: {Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}";
        }

        return TryGetManifestType(path, out var assetType)
            ? $"Asset Package: {Path.GetFileName(Path.GetDirectoryName(path))}"
            : $"Asset: {Path.GetFileName(path)}";
    }

    public static string GetType(string path, bool isDirectory)
    {
        if (isDirectory && TryFindManifest(path, out var manifestPath, out var assetType))
        {
            return $"{assetType} Package Folder";
        }

        if (isDirectory)
        {
            return "Asset Folder";
        }

        return TryGetManifestType(path, out var fileAssetType) ? $"{fileAssetType} Manifest" : "Asset File";
    }

    private static void BuildFolderRows(ICollection<InspectorPropertyRowViewModel> rows, EditorProject project, string path)
    {
        if (!Directory.Exists(path))
        {
            Add(rows, "Warning", "Status", "Folder does not exist.");
            return;
        }

        var directory = new DirectoryInfo(path);
        Add(rows, "Name", "Folder", directory.Name);
        Add(rows, "Project path", "Folder", ToProjectPath(project, path));

        var childFolderCount = SafeCount(() => Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly));
        var fileCount = SafeCount(() => Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly));
        Add(rows, "Child folders", "Folder", childFolderCount.ToString(CultureInfo.InvariantCulture));
        Add(rows, "Files", "Folder", fileCount.ToString(CultureInfo.InvariantCulture));
        Add(rows, "Immediate items", "Folder", (childFolderCount + fileCount).ToString(CultureInfo.InvariantCulture));

        if (TryFindManifest(path, out var manifestPath, out var assetType))
        {
            Add(rows, "Asset package", "Folder", "Yes");
            BuildManifestRows(rows, project, manifestPath, assetType, includeFileMetadata: false);
        }
        else
        {
            Add(rows, "Asset package", "Folder", "No");
        }
    }

    private static void BuildManifestRows(ICollection<InspectorPropertyRowViewModel> rows, EditorProject project, string path, string assetType, bool includeFileMetadata = true)
    {
        var assetFolder = Path.GetDirectoryName(path) ?? string.Empty;
        Add(rows, "Display name", "Asset Package", Path.GetFileName(assetFolder));
        Add(rows, "Asset type", "Asset Package", assetType);
        Add(rows, "Manifest", "Asset Package", Path.GetFileName(path));
        Add(rows, "Asset folder", "Asset Package", ToProjectPath(project, assetFolder));

        if (includeFileMetadata)
        {
            AddFileMetadata(rows, project, path, "Manifest File");
        }

        JsonDocument? manifest = null;
        try
        {
            manifest = JsonDocument.Parse(File.ReadAllText(path));
            AddJsonValue(rows, manifest.RootElement, ["name", "displayName", "title"], "Name", assetType);
            AddJsonValue(rows, manifest.RootElement, ["width", "panelWidth"], "Width", assetType);
            AddJsonValue(rows, manifest.RootElement, ["height", "panelHeight"], "Height", assetType);
            AddCount(rows, manifest.RootElement, ["lamps", "LampElements"], "Lamps", assetType);
            AddCount(rows, manifest.RootElement, ["reels", "Reels"], "Reels", assetType);
            AddCount(rows, manifest.RootElement, ["segments", "sevenSegmentDisplays", "alphaDisplays"], "Segments/displays", assetType);
            AddJsonValue(rows, manifest.RootElement, ["artwork", "artworkPath", "background", "backgroundImage", "image"], "Artwork/background", assetType);
            AddJsonValue(rows, manifest.RootElement, ["mask", "maskPath"], "Mask", assetType);
            AddJsonValue(rows, manifest.RootElement, ["model", "modelPath", "gltf", "glb"], "Model", assetType);
            AddJsonValue(rows, manifest.RootElement, ["panel", "panel2d", "face", "cabinet", "cabinet3d"], "Referenced asset", assetType);

            if (TryResolvePreviewPath(assetFolder, manifest.RootElement, out var previewPath))
            {
                TryAddImagePreview(rows, previewPath, "Preview");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Add(rows, "Warning", "Manifest", $"Could not read manifest: {ex.Message}");
        }
        finally
        {
            manifest?.Dispose();
        }
    }

    private static void BuildImageRows(ICollection<InspectorPropertyRowViewModel> rows, EditorProject project, string path)
    {
        AddFileMetadata(rows, project, path, "Image File");
        try
        {
            var image = LoadBitmap(path);
            Add(rows, "Dimensions", "Image", $"{image.PixelWidth:0} x {image.PixelHeight:0}");
            Add(rows, "Format", "Image", Path.GetExtension(path).TrimStart('.').ToUpperInvariant());
            Add(rows, "Pixel format", "Image", image.Format.ToString());
            rows.Add(new InspectorImagePreviewPropertyViewModel("Preview", "Image", image));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException)
        {
            Add(rows, "Warning", "Image", $"Could not load image preview: {ex.Message}");
        }
    }

    private static void BuildGenericFileRows(ICollection<InspectorPropertyRowViewModel> rows, EditorProject project, string path) => AddFileMetadata(rows, project, path, "File");

    private static void AddFileMetadata(ICollection<InspectorPropertyRowViewModel> rows, EditorProject project, string path, string group)
    {
        var file = new FileInfo(path);
        Add(rows, "Name", group, file.Name);
        Add(rows, "Extension", group, string.IsNullOrWhiteSpace(file.Extension) ? "(none)" : file.Extension);
        Add(rows, "Project path", group, ToProjectPath(project, path));
        Add(rows, "Size", group, FormatBytes(file.Length));
        Add(rows, "Modified", group, file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture));
    }

    private static bool TryAddImagePreview(ICollection<InspectorPropertyRowViewModel> rows, string path, string displayName)
    {
        if (!File.Exists(path) || !IsSupportedImage(path)) return false;
        try { rows.Add(new InspectorImagePreviewPropertyViewModel(displayName, "Preview", LoadBitmap(path), ToProjectPathFallback(path))); return true; }
        catch { return false; }
    }

    private static BitmapSource LoadBitmap(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static bool TryResolvePreviewPath(string assetFolder, JsonElement root, out string previewPath)
    {
        foreach (var key in new[] { "preview", "previewImage", "artwork", "artworkPath", "background", "backgroundImage", "image" })
        {
            if (TryFindString(root, key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                previewPath = Path.IsPathRooted(value) ? value : Path.GetFullPath(Path.Combine(assetFolder, value.Replace('/', Path.DirectorySeparatorChar)));
                return true;
            }
        }

        foreach (var fileName in new[] { "preview.png", "artwork.png", "background.png" })
        {
            var candidate = Path.Combine(assetFolder, fileName);
            if (File.Exists(candidate)) { previewPath = candidate; return true; }
        }

        previewPath = string.Empty;
        return false;
    }

    private static void AddJsonValue(ICollection<InspectorPropertyRowViewModel> rows, JsonElement root, string[] names, string displayName, string group)
    {
        foreach (var name in names)
        {
            if (TryFindScalar(root, name, out var value)) { Add(rows, displayName, group, value); return; }
        }
    }

    private static void AddCount(ICollection<InspectorPropertyRowViewModel> rows, JsonElement root, string[] names, string displayName, string group)
    {
        foreach (var name in names)
        {
            if (TryFindArrayCount(root, name, out var count)) { Add(rows, displayName, group, count.ToString(CultureInfo.InvariantCulture)); return; }
        }
    }

    private static bool TryFindScalar(JsonElement element, string name, out string value)
    {
        if (TryFindProperty(element, name, out var found) && found.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
        {
            value = found.ToString(); return true;
        }
        value = string.Empty; return false;
    }

    private static bool TryFindString(JsonElement element, string name, out string value)
    {
        if (TryFindProperty(element, name, out var found) && found.ValueKind == JsonValueKind.String) { value = found.GetString() ?? string.Empty; return true; }
        value = string.Empty; return false;
    }

    private static bool TryFindArrayCount(JsonElement element, string name, out int count)
    {
        if (TryFindProperty(element, name, out var found) && found.ValueKind == JsonValueKind.Array) { count = found.GetArrayLength(); return true; }
        count = 0; return false;
    }

    private static bool TryFindProperty(JsonElement element, string name, out JsonElement found)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) { found = property.Value; return true; }
                if (TryFindProperty(property.Value, name, out found)) return true;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) if (TryFindProperty(item, name, out found)) return true;
        }
        found = default; return false;
    }

    private static bool TryFindManifest(string directory, out string manifestPath, out string assetType)
    {
        foreach (var fileName in KnownManifestFiles)
        {
            var candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate) && TryGetManifestType(candidate, out assetType)) { manifestPath = candidate; return true; }
        }
        manifestPath = string.Empty; assetType = string.Empty; return false;
    }

    private static bool TryGetManifestType(string path, out string assetType)
    {
        assetType = Path.GetFileName(path).ToLowerInvariant() switch
        {
            ProjectAssetPathService.Panel2DManifestFileName => "Panel2D",
            ProjectAssetPathService.FaceManifestFileName => "Face",
            ProjectAssetPathService.Cabinet3DManifestFileName => "Cabinet3D",
            "asset.machine" => "Machine",
            _ => string.Empty
        };
        return assetType.Length > 0;
    }

    private static bool IsSupportedImage(string path) => Path.GetExtension(path).ToLowerInvariant() is ".png" or ".bmp" or ".jpg" or ".jpeg";
    private static void Add(ICollection<InspectorPropertyRowViewModel> rows, string name, string group, string value) => rows.Add(new InspectorInfoPropertyViewModel(name, group, value));
    private static int SafeCount(Func<string[]> read) { try { return read().Length; } catch { return 0; } }
    private static string ToProjectPath(EditorProject project, string path) => ProjectAssetPathService.NormalizeProjectRelativePath(Path.GetRelativePath(project.ProjectDirectory, Path.GetFullPath(path)));
    private static string ToProjectPathFallback(string path) => Path.GetFileName(path);
    private static string FormatBytes(long bytes) => bytes < 1024 ? $"{bytes} B" : bytes < 1024 * 1024 ? $"{bytes / 1024d:0.#} KB" : $"{bytes / 1024d / 1024d:0.#} MB";
}
