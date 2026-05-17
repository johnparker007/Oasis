using Xunit;

namespace OasisEditor.Tests;

public sealed class InputMapDiagnosticsServiceTests
{
    [Fact]
    public void Analyze_UnresolvableInput_ProducesUnresolvedTargetWarning()
    {
        var service = new InputMapDiagnosticsService(new MameInputPortResolver());
        var inputs = new[]
        {
            new InputDefinitionModel { Id = "a", Name = "Bad", Kind = InputDefinitionKind.Button, ButtonNumber = "ABC" }
        };

        var diagnostics = service.Analyze(FruitMachinePlatformType.MPU4, inputs);

        var warning = Assert.Single(diagnostics);
        Assert.Equal("input.unresolved_target", warning.Code);
        Assert.Equal("a", warning.InputDefinitionId);
        Assert.Equal(InputMapDiagnosticSeverity.Warning, warning.Severity);
    }

    [Fact]
    public void Analyze_DuplicateShortcut_ProducesWarningForEachInput()
    {
        var service = new InputMapDiagnosticsService(new MameInputPortResolver());
        var inputs = new[]
        {
            new InputDefinitionModel { Id = "a", Name = "A", Kind = InputDefinitionKind.Button, ButtonNumber = "1", KeyboardShortcut = "Space" },
            new InputDefinitionModel { Id = "b", Name = "B", Kind = InputDefinitionKind.Button, ButtonNumber = "2", KeyboardShortcut = "Space" }
        };

        var diagnostics = service.Analyze(FruitMachinePlatformType.MPU4, inputs);

        var duplicate = diagnostics.Where(d => d.Code == "input.duplicate_shortcut").ToList();
        Assert.Equal(2, duplicate.Count);
        Assert.Contains(duplicate, d => d.InputDefinitionId == "a");
        Assert.Contains(duplicate, d => d.InputDefinitionId == "b");
    }
}
