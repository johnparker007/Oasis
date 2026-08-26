using System.IO;
using SkiaSharp;

namespace OasisEditor;

internal static class FaceArtworkOverrideAssetService
{
    public static FaceArtworkOverrideModel Import(string sourcePath, EditorProject project, string faceName,
        FaceArtworkOverrideModel? alignment = null)
    {
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Artwork Override image was not found.", sourcePath);
        int width;int height;
        using (var codec = SKCodec.Create(sourcePath) ?? throw new InvalidDataException("Artwork Override image could not be inspected."))
        {width=codec.Info.Width;height=codec.Info.Height;}
        var safeFace = new ProjectAssetPathService().SanitizePathSegment(faceName);
        var directory = Path.Combine(project.AssetsDirectory, "Faces", safeFace, "ArtworkOverride");
        Directory.CreateDirectory(directory);
        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        var destination = Path.Combine(directory, $"override{extension}");
        if (!Path.GetFullPath(sourcePath).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
            File.Copy(sourcePath, destination, true);
        return Create(destination, project, width, height, alignment);
    }

    public static FaceArtworkOverrideModel CreateFromBase(FaceArtworkModel artwork, EditorProject project, string faceName)
    {
        if (string.IsNullOrWhiteSpace(artwork.BaseAssetPath)) throw new InvalidOperationException("Base Artwork is not configured.");
        var source = FaceArtworkGeneratedPathService.Resolve(artwork.BaseAssetPath, project.ProjectDirectory);
        return Import(source, project, faceName);
    }

    public static FaceArtworkOverrideModel Reload(FaceArtworkOverrideModel value, EditorProject project)
    {
        var path = FaceArtworkGeneratedPathService.Resolve(value.AssetPath, project.ProjectDirectory);
        using var codec = SKCodec.Create(path) ?? throw new InvalidDataException("Artwork Override image could not be inspected.");
        return new FaceArtworkOverrideModel
        {
            Enabled=value.Enabled, AssetPath=value.AssetPath, PixelWidth=codec.Info.Width, PixelHeight=codec.Info.Height,
            PerspectiveRegistration=value.PerspectiveRegistration,
            X=value.X, Y=value.Y, Width=value.Width, Height=value.Height, ContentRevision=value.ContentRevision + 1
        };
    }

    private static FaceArtworkOverrideModel Create(string path, EditorProject project, int width, int height,
        FaceArtworkOverrideModel? alignment) => new()
    {
        Enabled=true,
        AssetPath=ProjectAssetPathService.NormalizeProjectRelativePath(Path.GetRelativePath(project.ProjectDirectory, path)),
        PixelWidth=width, PixelHeight=height, X=alignment?.X ?? 0d, Y=alignment?.Y ?? 0d,
        PerspectiveRegistration=FacePerspectiveRegistrationModel.FullImage,
        Width=alignment?.Width ?? 1d, Height=alignment?.Height ?? 1d,
        ContentRevision=(alignment?.ContentRevision ?? 0) + 1
    };
}
