using System.IO;

namespace OasisEditor;

/// <summary>Owns the generated artwork stage names and project-path resolution.</summary>
internal static class FaceArtworkGeneratedPathService
{
    public const string BaseFileName = "base.png";
    public const string OutputFileName = "artwork.png";
    public const string CorrectionInputFileName = "correction-input.png";

    public static string GetBasePathFromOutput(string outputPath) =>
        Path.Combine(Path.GetDirectoryName(outputPath) ?? string.Empty, BaseFileName);

    public static string GetCorrectionInputPathFromOutput(string outputPath) =>
        Path.Combine(Path.GetDirectoryName(outputPath) ?? string.Empty, CorrectionInputFileName);

    public static string Resolve(string path, string projectDirectory) => Path.IsPathRooted(path)
        ? path
        : Path.Combine(projectDirectory, path.Replace('/', Path.DirectorySeparatorChar));

    public static string ToProjectRelative(string path, string projectDirectory) =>
        Path.GetRelativePath(projectDirectory, path).Replace('\\', '/');
}
