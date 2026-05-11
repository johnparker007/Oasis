using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MameProcessStartInfoBuilderTests
{
    [Fact]
    public void Build_IncludesRuntimeArgumentsForHeadlessOasisMode()
    {
        var builder = new MameProcessStartInfoBuilder();
        var request = new MameProcessLaunchRequest(
            @"C:\Mame\mame.exe",
            "myrom",
            @"C:\Users\john\AppData\Local\OasisEditor\MAME\roms",
            @"C:\Mame\plugins",
            string.Empty);

        var startInfo = builder.Build(request);

        Assert.Equal(@"C:\Mame\mame.exe", startInfo.FileName);
        Assert.Contains("myrom", startInfo.Arguments);
        Assert.Contains("-rompath", startInfo.Arguments);
        Assert.Contains(@"C:\Users\john\AppData\Local\OasisEditor\MAME\roms", startInfo.Arguments);
        Assert.Contains("-output console", startInfo.Arguments);
        Assert.Contains("-plugin oasis", startInfo.Arguments);
        Assert.Contains("-skip_gameinfo", startInfo.Arguments);
        Assert.Contains("-video none", startInfo.Arguments);
        Assert.Contains("-seconds_to_run 999999999", startInfo.Arguments);
        Assert.DoesNotContain("-plugins_path", startInfo.Arguments);
    }

    [Fact]
    public void Build_AppendsAdditionalArguments()
    {
        var builder = new MameProcessStartInfoBuilder();
        var request = new MameProcessLaunchRequest(
            @"C:\Mame\mame.exe",
            "myrom",
            @"C:\roms",
            @"C:\plugins",
            "-window -verbose");

        var startInfo = builder.Build(request);

        Assert.EndsWith("-window -verbose", startInfo.Arguments);
    }

    [Fact]
    public void Build_ConfiguresHiddenRedirectedProcess()
    {
        var builder = new MameProcessStartInfoBuilder();
        var request = new MameProcessLaunchRequest(
            @"C:\Mame\mame.exe",
            "myrom",
            @"C:\roms",
            @"C:\plugins",
            string.Empty);

        var startInfo = builder.Build(request);

        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.RedirectStandardInput);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.True(startInfo.CreateNoWindow);
        Assert.Equal(System.Diagnostics.ProcessWindowStyle.Hidden, startInfo.WindowStyle);
        Assert.Equal(@"C:\Mame", startInfo.WorkingDirectory);
    }
}
