using System.Diagnostics;
using OasisEditor.Features.MachineEditor.Models;
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

        var result = service.Preview(project, "Assets/Machines/Test Machine/asset.machine", MachineDocumentExtensions.Empty("Test Machine"), new OasisPlayerPreferences { ExecutablePath = exe }, NoOpEditorProgressReporter.Instance, CancellationToken.None);

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

        var result = service.Preview(project, "Assets/Machines/Test Machine/asset.machine", MachineDocumentExtensions.Empty("Test Machine"), new OasisPlayerPreferences { ExecutablePath = exe, PreviewWidth = 1600, PreviewHeight = 900 }, NoOpEditorProgressReporter.Instance, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(exactBuildRoot, result.BuildRoot);
        Assert.Equal(Path.GetFullPath(exactBuildRoot), starter.StartInfo!.ArgumentList[3]);
    }

    private static EditorProject CreateProject()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "Oasis Player Tests", Guid.NewGuid().ToString("N"))).FullName;
        return TestProjectFactory.Create(root, "TestProject");
    }

    private sealed class StubBuildService : IMachineRuntimeBuildService
    {
        private readonly MachineRuntimeBuildResult _result;
        public StubBuildService(MachineRuntimeBuildResult result) => _result = result;
        public MachineRuntimeBuildResult BuildFromMachineDocument(EditorProject project, string machineManifestPath, MachineDocument machineDocument, IEditorProgressReporter progress, CancellationToken cancellationToken) => _result;
    }

    private sealed class CapturingStarter : IOasisPlayerProcessStarter
    {
        public ProcessStartInfo? StartInfo { get; private set; }
        public void Start(ProcessStartInfo startInfo) => StartInfo = startInfo;
    }
}
