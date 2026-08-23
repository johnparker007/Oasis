using System.IO;
using SkiaSharp;

namespace OasisEditor;

internal sealed record FaceArtworkImageImportResult(string AssetPath, int Width, int Height);

internal static class FaceArtworkImageImportService
{
    public static FaceArtworkImageImportResult Import(string externalPath, EditorProject project, string faceName)
    {
        if (!File.Exists(externalPath)) throw new FileNotFoundException("Artwork source image was not found.", externalPath);
        using var bitmap = SKBitmap.Decode(externalPath) ?? throw new InvalidDataException("Artwork source image could not be decoded.");
        var safeFace = new ProjectAssetPathService().SanitizePathSegment(faceName);
        var directory = Path.Combine(project.AssetsDirectory, "Faces", safeFace, "ArtworkSource");
        Directory.CreateDirectory(directory);
        var fileName = Path.GetFileName(externalPath);
        var destination = Path.Combine(directory, fileName);
        if (!Path.GetFullPath(externalPath).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(destination)) destination = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(fileName)}-{Guid.NewGuid():N}{Path.GetExtension(fileName)}");
            File.Copy(externalPath, destination, false);
        }
        return new FaceArtworkImageImportResult(ProjectAssetPathService.NormalizeProjectRelativePath(Path.GetRelativePath(project.ProjectDirectory, destination)), bitmap.Width, bitmap.Height);
    }
}
