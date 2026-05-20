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
        var result = HeadlessAutomationCli.Parse(["--headless", "convert-mfme"]);
        Assert.True(result.IsHeadless);
        Assert.Equal(HeadlessExitCode.InvalidArguments, result.ErrorCode);
    }
}
