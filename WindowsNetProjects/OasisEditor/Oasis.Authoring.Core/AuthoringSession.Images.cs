using SkiaSharp;

namespace OasisEditor;

public sealed partial class AuthoringSession
{
    /// <summary>Import an image from the explicit workspace into the active authored panel package.</summary>
    public string ImportImage(string sourcePath, string? name = null)
    {
        var document = RequireDocument();
        var root = RequireProject();
        var fullSource = Path.GetFullPath(sourcePath, _workspacePath);
        var source = WorkspacePathPolicy.ResolveConfined(_workspacePath, Path.GetRelativePath(_workspacePath, fullSource), true);
        using var codec = SKCodec.Create(source) ?? throw new InvalidDataException("The selected file is not a supported image.");
        if (codec.Info.Width is < 1 or > 8192 || codec.Info.Height is < 1 or > 8192)
            throw new InvalidDataException("Imported images must be between 1 and 8192 pixels in each dimension.");
        using var bitmap = SKBitmap.Decode(codec) ?? throw new InvalidDataException("The image could not be decoded.");
        var id = Guid.NewGuid().ToString("N");
        var relativeDirectory = Path.GetRelativePath(root, Path.Combine(Path.GetDirectoryName(_panelPath!)!, "Images"));
        var directory = WorkspacePathPolicy.ResolveConfined(root, relativeDirectory);
        Directory.CreateDirectory(directory);
        var relative = Path.Combine(relativeDirectory, id + ".png");
        var destination = WorkspacePathPolicy.ResolveConfined(root, relative);
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100) ?? throw new InvalidDataException("The image could not be encoded.");
        var element = new PanelElementFile
        {
            ObjectId = id, Name = string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(source) : name.Trim(),
            Kind = "image", X = 0, Y = 0, Width = bitmap.Width, Height = bitmap.Height,
            AssetPath = relative.Replace('\\', '/'), IsVisible = true
        };
        var owned = false;
        try
        {
            using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write)) { owned = true; encoded.SaveTo(output); }
            Commit(document with { Elements = [.. document.Elements, element] }, notify: false);
        }
        catch
        {
            // Only this fresh ID's resource is ours to remove; existing authored files are never touched.
            if (owned && File.Exists(destination)) File.Delete(destination);
            throw;
        }
        _selectedIds = [id]; _selectedId = id;
        Changed?.Invoke();
        return id;
    }
}
