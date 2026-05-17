using Xunit;

namespace OasisEditor.Tests;

public sealed class InputMapDiagnosticsServiceCombinedWarningsTests
{
    [Fact]
    public void Analyze_MultipleWarningConditions_AggregatesAllExpectedDiagnostics()
    {
        var service = new InputMapDiagnosticsService(new MameInputPortResolver());
        var sharedVisualId = Guid.NewGuid();
        var inputs = new[]
        {
            new InputDefinitionModel { Id = "a", Name = "A", Kind = InputDefinitionKind.Button, ButtonNumber = "ABC", KeyboardShortcut = "Space", LinkedVisualElementId = sharedVisualId },
            new InputDefinitionModel { Id = "b", Name = "B", Kind = InputDefinitionKind.Button, ButtonNumber = "2", KeyboardShortcut = "space", LinkedVisualElementId = sharedVisualId }
        };

        var diagnostics = service.Analyze(FruitMachinePlatformType.MPU4, inputs);

        Assert.Equal(5, diagnostics.Count);
        Assert.Equal(1, diagnostics.Count(d => d.Code == "input.unresolved_target"));
        Assert.Equal(2, diagnostics.Count(d => d.Code == "input.duplicate_shortcut"));
        Assert.Equal(2, diagnostics.Count(d => d.Code == "input.duplicate_linked_visual"));
        Assert.All(diagnostics, d => Assert.Equal(InputMapDiagnosticSeverity.Warning, d.Severity));
    }
}
