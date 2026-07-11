using System.IO;
using OasisEditor.Automation;
using Xunit;

namespace OasisEditor.Tests;

public sealed class HeadlessAutomationCliTests
{
    [Fact]
    public void Parse_NoHeadlessFlag_ReturnsNotHeadless()
    {
        var result = HeadlessAutomationCli.Parse([]);
        Assert.False(result.IsHeadless);
    }

    [Fact]
    public void Parse_HeadlessMissingRequiredArgs_ReturnsInvalidArguments()
    {
        var result = HeadlessAutomationCli.Parse(["--headless", "convert-fml"]);
        Assert.True(result.IsHeadless);
        Assert.Equal(HeadlessExitCode.InvalidArguments, result.ErrorCode);
    }

    [Fact]
    public void Parse_WithValidRequiredArgs_ResolvesOptions()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"oasis-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var inputPath = Path.Combine(tempDir, "layout.fml");
        File.WriteAllText(inputPath, "{}");
        var projectPath = Path.Combine(tempDir, "demo.oasisproj");

        try
        {
            var result = HeadlessAutomationCli.Parse([
                "--headless", "convert-fml",
                "--input", inputPath,
                "--project", projectPath,
                "--panel", "fmlimport.panel2d",
                "--export-lay", "out.lay"]);

            Assert.True(result.IsHeadless);
            Assert.NotNull(result.Options);
            Assert.Equal(HeadlessExitCode.Success, result.ErrorCode);
            Assert.Equal("out.lay", result.Options!.ExportLayPath);
            Assert.Equal(Path.Combine(tempDir, "Assets", "fmlimport.panel2d"), result.Options.OutputPanelPath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
