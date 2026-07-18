using System.ComponentModel;
using System.Diagnostics;
using Xunit;

namespace OasisEditor.Tests;

public sealed class OasisPlayerLaunchServiceTests
{
    [Fact]
    public void Launch_MissingExecutable_ReturnsPreferencesGuidance()
    {
        var result = new OasisPlayerLaunchService(new CapturingStarter()).Launch(new OasisPlayerLaunchRequest("", "C:\\Build", false, 1280, 800));

        Assert.False(result.Success);
        Assert.Contains("Preferences > Player", result.ErrorMessage);
    }

    [Fact]
    public void Launch_DirectoryExecutable_ReturnsClearFailure()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "Oasis Player Tests", Guid.NewGuid().ToString("N"))).FullName;

        var result = new OasisPlayerLaunchService(new CapturingStarter()).Launch(new OasisPlayerLaunchRequest(directory, "C:\\Build", false, 1280, 800));

        Assert.False(result.Success);
        Assert.Contains("points to a directory", result.ErrorMessage);
    }

    [Fact]
    public void Launch_MissingExecutable_ReturnsClearFailure()
    {
        var missing = Path.Combine(Path.GetTempPath(), "Oasis Player Tests", Guid.NewGuid().ToString("N"), "OasisPlayer.exe");

        var result = new OasisPlayerLaunchService(new CapturingStarter()).Launch(new OasisPlayerLaunchRequest(missing, "C:\\Build", false, 1280, 800));

        Assert.False(result.Success);
        Assert.Contains("does not exist", result.ErrorMessage);
    }

    [Theory]
    [InlineData(0, 800, "width")]
    [InlineData(1280, 0, "height")]
    public void Launch_InvalidSize_ReturnsClearFailure(int width, int height, string expectedTerm)
    {
        var result = new OasisPlayerLaunchService(new CapturingStarter()).Launch(new OasisPlayerLaunchRequest("C:\\OasisPlayer.exe", "C:\\Build", false, width, height));

        Assert.False(result.Success);
        Assert.Contains(expectedTerm, result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Launch_WindowedArguments_PassSeparateArgumentsAndPreserveSpaces()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "Oasis Player Tests", Guid.NewGuid().ToString("N"))).FullName;
        var exe = Path.Combine(root, "Oasis Player.exe");
        var build = Path.Combine(root, "Build With Spaces");
        File.WriteAllText(exe, string.Empty);
        Directory.CreateDirectory(build);
        var starter = new CapturingStarter();

        var result = new OasisPlayerLaunchService(starter).Launch(new OasisPlayerLaunchRequest(exe, build, false, 1440, 900));

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(starter.StartInfo);
        Assert.Equal(Path.GetFullPath(exe), starter.StartInfo!.FileName);
        Assert.Equal(new[] { "--mode", "machine-preview", "--build", Path.GetFullPath(build), "--windowed", "--width", "1440", "--height", "900" }, starter.StartInfo.ArgumentList);
    }

    [Fact]
    public void Launch_FullscreenArguments_UseFullscreenInsteadOfWindowed()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "Oasis Player Tests", Guid.NewGuid().ToString("N"))).FullName;
        var exe = Path.Combine(root, "OasisPlayer.exe");
        File.WriteAllText(exe, string.Empty);
        var starter = new CapturingStarter();

        var result = new OasisPlayerLaunchService(starter).Launch(new OasisPlayerLaunchRequest(exe, root, true, 1280, 800));

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains("--fullscreen", starter.StartInfo!.ArgumentList);
        Assert.DoesNotContain("--windowed", starter.StartInfo.ArgumentList);
    }

    [Fact]
    public void Launch_ProcessException_ReturnsClearFailure()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "Oasis Player Tests", Guid.NewGuid().ToString("N"))).FullName;
        var exe = Path.Combine(root, "OasisPlayer.exe");
        File.WriteAllText(exe, string.Empty);

        var result = new OasisPlayerLaunchService(new ThrowingStarter()).Launch(new OasisPlayerLaunchRequest(exe, root, false, 1280, 800));

        Assert.False(result.Success);
        Assert.Contains("Failed to launch Oasis Player", result.ErrorMessage);
        Assert.Contains("blocked", result.ErrorMessage);
    }

    private sealed class CapturingStarter : IOasisPlayerProcessStarter
    {
        public ProcessStartInfo? StartInfo { get; private set; }
        public void Start(ProcessStartInfo startInfo) => StartInfo = startInfo;
    }

    private sealed class ThrowingStarter : IOasisPlayerProcessStarter
    {
        public void Start(ProcessStartInfo startInfo) => throw new Win32Exception("blocked");
    }
}
