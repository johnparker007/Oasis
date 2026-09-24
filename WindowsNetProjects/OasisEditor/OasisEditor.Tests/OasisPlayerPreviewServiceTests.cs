using System.Diagnostics;
using OasisEditor.Progress;
using Xunit;

namespace OasisEditor.Tests;

public sealed class OasisPlayerPreviewServiceTests
{
    [Fact]
    public void Preview_BuildFailure_PreventsLaunch()
    {
        var project = CreateProject();
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "Oasis Player Tests", Guid.NewGuid().ToString("N"))).FullName;
        var exe = Path.Combine(root, "OasisPlayer.exe");
        File.WriteAllText(exe, string.Empty);
        var starter = new CapturingStarter();
        var service = new OasisPlayerPreviewService(new StubBuildService(MachineRuntimeBuildResult.Fail("machine build failed")), new OasisPlayerLaunchService(starter));

        var result = service.Preview(project, "asset.machine", MachineDocument.Create("Test"), new OasisPlayerPreferences { ExecutablePath = exe }, NoOpEditorProgressReporter.Instance, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("machine build failed", result.ErrorMessage);
        Assert.Null(starter.StartInfo);
    }

    [Fact]
    public void Preview_SuccessfulBuild_PassesExactReturnedBuildRoot()
    {
        var project = CreateProject();
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "Oasis Player Tests", Guid.NewGuid().ToString("N"))).FullName;
        var exe = Path.Combine(root, "OasisPlayer.exe");
        var exactBuildRoot = Path.Combine(root, "Exact Build Root With Spaces");
        File.WriteAllText(exe, string.Empty);
        Directory.CreateDirectory(exactBuildRoot);
        var starter = new CapturingStarter();
        var service = new OasisPlayerPreviewService(new StubBuildService(MachineRuntimeBuildResult.Ok(exactBuildRoot)), new OasisPlayerLaunchService(starter));

        var result = service.Preview(project, "asset.machine", MachineDocument.Create("Test"), new OasisPlayerPreferences { ExecutablePath = exe, PreviewWidth = 1600, PreviewHeight = 900 }, NoOpEditorProgressReporter.Instance, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(exactBuildRoot, result.BuildRoot);
        Assert.Equal(Path.GetFullPath(exactBuildRoot), starter.StartInfo!.ArgumentList[3]);
    }

    [Fact]
    public void Preview_ResolvesBuildServiceFromCurrentSessionStateOnEveryInvocation()
    {
        var project = CreateProject();
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "Oasis Player Tests", Guid.NewGuid().ToString("N"))).FullName;
        var exe = Path.Combine(root, "OasisPlayer.exe"); File.WriteAllText(exe, string.Empty);
        var currentLibrary = "LibraryA";
        var requested = new List<string>();
        var service = new OasisPlayerPreviewService(() => { requested.Add(currentLibrary); return new StubBuildService(MachineRuntimeBuildResult.Fail("expected")); });
        service.Preview(project, "asset.machine", MachineDocument.Create("Test"), new OasisPlayerPreferences { ExecutablePath = exe }, NoOpEditorProgressReporter.Instance, CancellationToken.None);
        currentLibrary = "LibraryB";
        service.Preview(project, "asset.machine", MachineDocument.Create("Test"), new OasisPlayerPreferences { ExecutablePath = exe }, NoOpEditorProgressReporter.Instance, CancellationToken.None);
        Assert.Equal(["LibraryA", "LibraryB"], requested);
    }

    private static EditorProject CreateProject()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "Oasis Player Tests", Guid.NewGuid().ToString("N"))).FullName;
        return new EditorProject
        {
            Name = "TestProject",
            ProjectFilePath = Path.Combine(root, "TestProject.oasisproj"),
            ProjectDirectory = root,
            AssetsDirectory = Path.Combine(root, "Assets"),
            GeneratedDirectory = Path.Combine(root, "Generated")
        };
    }

    private sealed class StubBuildService : IMachineRuntimeBuildService
    {
        private readonly MachineRuntimeBuildResult _result;
        public StubBuildService(MachineRuntimeBuildResult result) => _result = result;
        public MachineRuntimeBuildResult BuildFromMachineDocument(EditorProject project, string machineManifestPath, IEditorProgressReporter progress, CancellationToken cancellationToken) => _result;
        public MachineRuntimeBuildResult BuildFromMachineDocument(EditorProject project, string machineManifestPath, MachineDocument machineDocument, IEditorProgressReporter progress, CancellationToken cancellationToken) => _result;
    }

    private sealed class CapturingStarter : IOasisPlayerProcessStarter
    {
        public ProcessStartInfo? StartInfo { get; private set; }
        public void Start(ProcessStartInfo startInfo) => StartInfo = startInfo;
    }
}
