using System.IO;

namespace OasisEditor;

/// <summary>Owns the generated artwork stage names and project-path resolution.</summary>
internal static class FaceArtworkGeneratedPathService
{
    public const string BaseFileName = FacePipeline.FaceArtworkGeneratedPathService.BaseFileName;
    public const string OutputFileName = FacePipeline.FaceArtworkGeneratedPathService.OutputFileName;
    public const string CorrectionInputFileName = FacePipeline.FaceArtworkGeneratedPathService.CorrectionInputFileName;

    public static string GetBasePathFromOutput(string outputPath)
        => FacePipeline.FaceArtworkGeneratedPathService.GetBasePathFromOutput(outputPath);

    public static string GetCorrectionInputPathFromOutput(string outputPath)
        => FacePipeline.FaceArtworkGeneratedPathService.GetCorrectionInputPathFromOutput(outputPath);

    public static string Resolve(string path, string projectDirectory)
        => FacePipeline.FaceArtworkGeneratedPathService.Resolve(path, projectDirectory);

    public static string ToProjectRelative(string path, string projectDirectory) =>
        FacePipeline.FaceArtworkGeneratedPathService.ToProjectRelative(path, projectDirectory);
}
