using Xunit;

namespace OasisEditor.Tests;

public sealed class InputMapDiagnosticsServiceCaseInsensitiveTests
{
    [Fact]
    public void Analyze_ShortcutsDifferOnlyByCase_AreReportedAsDuplicates()
    {
        var service = new InputMapDiagnosticsService(new MameInputPortResolver());
        var inputs = new[]
        {
            new InputDefinitionModel { Id = "a", Name = "A", Kind = InputDefinitionKind.Button, ButtonNumber = "1", KeyboardShortcut = "Space" },
            new InputDefinitionModel { Id = "b", Name = "B", Kind = InputDefinitionKind.Button, ButtonNumber = "2", KeyboardShortcut = "space" }
        };

        var diagnostics = service.Analyze(FruitMachinePlatformType.MPU4, inputs);

        var duplicate = diagnostics.Where(d => d.Code == "input.duplicate_shortcut").ToList();
        Assert.Equal(2, duplicate.Count);
        Assert.Contains(duplicate, d => d.InputDefinitionId == "a");
        Assert.Contains(duplicate, d => d.InputDefinitionId == "b");
    }
}
