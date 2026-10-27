using System.IO;
using OasisEditor.Progress;

namespace OasisEditor;

/// <summary>Creates a portable folder package from the production Machine runtime build without replacing a chosen destination.</summary>
public sealed class MachineRuntimePackageExportService
{
    private readonly MachineRuntimeBuildService _builder;

    public MachineRuntimePackageExportService(MachineRuntimeBuildService? builder = null) => _builder = builder ?? new MachineRuntimeBuildService();

    public MachineRuntimePackageExportResult Export(EditorProject project, string machineManifestPath, string destinationDirectory,
        IEditorProgressReporter progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(machineManifestPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        ArgumentNullException.ThrowIfNull(progress);
        cancellationToken.ThrowIfCancellationRequested();

        var manifest = Path.GetFullPath(machineManifestPath);
        var name = ProjectAssetPathService.GetPackageAssetNameFromManifestPath(manifest, EditorAssetType.Machine);
        if (string.IsNullOrWhiteSpace(name)) return MachineRuntimePackageExportResult.Fail("Machine manifests must be stored as Assets/Machines/<Name>/asset.machine before export.");
        var destinationRoot = Path.GetFullPath(destinationDirectory);
        var output = Path.Combine(destinationRoot, name);
        var staging = Path.Combine(destinationRoot, "." + name + ".exporting-" + Guid.NewGuid().ToString("N"));
        try
        {
            if (File.Exists(output) || Directory.Exists(output)) return MachineRuntimePackageExportResult.Fail($"Export destination already exists: {output}");
            RejectReparsePoint(destinationRoot);
            var built = _builder.BuildFromMachineDocument(project, manifest, progress.CreateChild(0, .7), cancellationToken);
            if (!built.Success) return MachineRuntimePackageExportResult.Fail(built.ErrorMessage ?? "Machine build failed.");
            var source = Path.GetFullPath(built.BuildRoot!);
            if (!Directory.Exists(source)) throw new DirectoryNotFoundException($"Build output was not found: {source}");
            if (IsWithin(source, destinationRoot) || IsWithin(source, output) || IsWithin(destinationRoot, source) || IsWithin(output, source))
                throw new IOException("The export destination must be separate from the generated Player build directory.");
            // Recheck after build in case the destination appeared or a parent changed while the build ran.
            RejectReparsePoint(destinationRoot);
            RejectTreeReparsePoints(source);
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(staging);
            var files = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).ToArray();
            for (var index = 0; index < files.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(source, files[index]);
                var target = Path.Combine(staging, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(files[index], target, overwrite: false);
                progress.Report(.7 + .3 * (index + 1d) / Math.Max(1, files.Length), $"Packaging {relative}...");
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(output) || Directory.Exists(output)) return MachineRuntimePackageExportResult.Fail($"Export destination already exists: {output}");
            Directory.Move(staging, output);
            progress.Report(1, "Portable Player package exported.");
            return MachineRuntimePackageExportResult.Ok(output, source);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            return MachineRuntimePackageExportResult.Fail($"Failed to export portable Player package: {exception.Message}");
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    private static void RejectReparsePoint(string path)
    {
        try { WorkspacePathPolicy.RejectRootReparsePoints(path, requireExists: false); }
        catch (InvalidDataException exception) { throw new IOException($"Export root is not a safe directory: {exception.Message}", exception); }
    }

    private static bool IsWithin(string candidate, string root)
    {
        var fullCandidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(fullCandidate, fullRoot, comparison)
            || fullCandidate.StartsWith(fullRoot + Path.DirectorySeparatorChar, comparison);
    }

    private static void RejectTreeReparsePoints(string root)
    {
        RejectReparsePoint(root);
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(pending.Pop()))
            {
                var attributes = File.GetAttributes(path);
                if (attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException($"Build output contains a reparse point: {path}");
                if (attributes.HasFlag(FileAttributes.Directory)) pending.Push(path);
            }
        }
    }
}

public sealed record MachineRuntimePackageExportResult(bool Success, string? PackageDirectory, string? BuildRoot, string? ErrorMessage)
{
    public static MachineRuntimePackageExportResult Ok(string packageDirectory, string buildRoot) => new(true, packageDirectory, buildRoot, null);
    public static MachineRuntimePackageExportResult Fail(string message) => new(false, null, null, message);
}
