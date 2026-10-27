using System.ComponentModel;
using ModelContextProtocol.Server;
using OasisEditor.Progress;

namespace OasisEditor.Avalonia.Mcp;

public sealed partial class EditorTools
{
    private readonly SemaphoreSlim machineBuildGate = new(1, 1);

    [McpServerTool(Name = "build_machine_for_player"), Description("Build the saved Machine with the production Oasis Player runtime builder into the confined project Generated/Builds folder. Refuses dirty or externally changed documents and never launches a Player process.")]
    public async Task<object> BuildMachineForPlayer(CancellationToken cancellationToken, string? libraryRoot = null)
    {
        await machineBuildGate.WaitAsync(cancellationToken);
        try
        {
            var inputs = await ui.Invoke(() => GetMachineBuildInputs(libraryRoot), cancellationToken);
            var builder = new MachineRuntimeBuildService(libraryRoot: inputs.LibraryRoot);
            var buildRoot = builder.GetBuildRoot(inputs.Project, Path.GetFileName(Path.GetDirectoryName(inputs.MachinePath)!));
            EnsureGeneratedOutput(inputs.Project, buildRoot);
            var result = await Task.Run(() => builder.BuildFromMachineDocument(inputs.Project, inputs.MachinePath, NoOpEditorProgressReporter.Instance, cancellationToken), cancellationToken);
            if (result.Success) await EnsureInputsUnchangedAsync(inputs.MachinePath, libraryRoot, cancellationToken);
            return new { result.Success, result.BuildRoot, result.ErrorMessage, generatedRoot = inputs.Project.GeneratedDirectory, playerLaunched = false };
        }
        finally { machineBuildGate.Release(); }
    }

    [McpServerTool(Name = "export_machine_player_package"), Description("Build and export the actual portable Oasis Player runtime package under project Generated/Exports. Existing destinations are preserved and cause an error. Never launches a Player process.")]
    public async Task<object> ExportMachinePlayerPackage(CancellationToken cancellationToken, string? libraryRoot = null)
    {
        await machineBuildGate.WaitAsync(cancellationToken);
        try
        {
            var inputs = await ui.Invoke(() => GetMachineBuildInputs(libraryRoot), cancellationToken);
            var exportRoot = WorkspacePathPolicy.ResolveConfined(inputs.Project.ProjectDirectory, "Generated/Exports", requireExists: false);
            var packageService = new MachineRuntimePackageExportService(new MachineRuntimeBuildService(libraryRoot: inputs.LibraryRoot));
            var result = await Task.Run(() => packageService.Export(inputs.Project, inputs.MachinePath, exportRoot, NoOpEditorProgressReporter.Instance, cancellationToken), cancellationToken);
            if (result.Success) await EnsureInputsUnchangedAsync(inputs.MachinePath, libraryRoot, cancellationToken);
            return new { result.Success, result.PackageDirectory, result.BuildRoot, result.ErrorMessage, generatedRoot = inputs.Project.GeneratedDirectory, playerLaunched = false };
        }
        finally { machineBuildGate.Release(); }
    }

    private async Task EnsureInputsUnchangedAsync(string machinePath, string? libraryRoot, CancellationToken cancellationToken)
    {
        await ui.Invoke(() =>
        {
            var current = GetMachineBuildInputs(libraryRoot);
            if (!string.Equals(Path.GetFullPath(current.MachinePath), Path.GetFullPath(machinePath), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new InvalidOperationException("The active project or Machine changed while build/export was running; the generated output must be rebuilt for the current selection.");
            return true;
        }, cancellationToken);
    }

    private (EditorProject Project, string MachinePath, string LibraryRoot) GetMachineBuildInputs(string? requestedLibraryRoot)
    {
        if (session.IsDirty || faces.IsDirty || Machines.IsDirty)
            throw new InvalidOperationException("Save or undo all open document changes before building or exporting.");
        if (!Machines.IsProjectCurrent) throw new InvalidOperationException("The Machine is no longer bound to the active project. Reopen it before building.");
        Machines.EnsureSavedSourceUnchanged();
        var projectFile = session.ProjectFilePath ?? throw new InvalidOperationException("Open an Oasis project before building.");
        var machinePath = Machines.MachinePath ?? throw new InvalidOperationException("Open or create a Machine before building.");
        var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectFile))!;
        var project = new EditorProject
        {
            Name = session.ProjectName ?? Path.GetFileName(projectDirectory), ProjectFilePath = Path.GetFullPath(projectFile),
            ProjectDirectory = projectDirectory, AssetsDirectory = Path.Combine(projectDirectory, "Assets"),
            GeneratedDirectory = WorkspacePathPolicy.ResolveConfined(projectDirectory, "Generated", requireExists: false)
        };
        var libraryRoot = string.IsNullOrWhiteSpace(requestedLibraryRoot) ? string.Empty : Path.GetFullPath(requestedLibraryRoot);
        if (!string.IsNullOrEmpty(libraryRoot) && !Directory.Exists(libraryRoot)) throw new DirectoryNotFoundException($"Oasis Library folder was not found: {libraryRoot}");
        return (project, machinePath, libraryRoot);
    }

    private static void EnsureGeneratedOutput(EditorProject project, string outputPath)
    {
        var generatedRelative = Path.GetRelativePath(project.ProjectDirectory, project.GeneratedDirectory);
        var outputRelative = Path.GetRelativePath(project.GeneratedDirectory, outputPath);
        _ = WorkspacePathPolicy.ResolveConfined(project.ProjectDirectory, generatedRelative, requireExists: false);
        _ = WorkspacePathPolicy.ResolveConfined(project.GeneratedDirectory, outputRelative, requireExists: false);
    }
}
