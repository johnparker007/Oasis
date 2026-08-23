using System.IO;
using SkiaSharp;

namespace OasisEditor;

internal static class FaceLampMaskImageImportService
{
    public static (string AssetPath, int Width, int Height) Import(string externalPath, EditorProject project, string faceName)
    {
        if (!File.Exists(externalPath)) throw new FileNotFoundException("Lamp-mask image was not found.", externalPath);
        using var bitmap=SKBitmap.Decode(externalPath) ?? throw new InvalidDataException("Lamp-mask image could not be decoded.");
        var safeFace=new ProjectAssetPathService().SanitizePathSegment(faceName);
        var directory=Path.Combine(project.AssetsDirectory,"Faces",safeFace,"Illumination");Directory.CreateDirectory(directory);
        var destination=Path.Combine(directory,"lamp-mask"+Path.GetExtension(externalPath).ToLowerInvariant());
        if(!Path.GetFullPath(externalPath).Equals(Path.GetFullPath(destination),StringComparison.OrdinalIgnoreCase))File.Copy(externalPath,destination,true);
        return(ProjectAssetPathService.NormalizeProjectRelativePath(Path.GetRelativePath(project.ProjectDirectory,destination)),bitmap.Width,bitmap.Height);
    }
}
