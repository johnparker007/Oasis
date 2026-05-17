using Xunit;

namespace OasisEditor.Tests;

public sealed class InputMapDiagnosticsServiceLinkedVisualTests
{
    [Fact]
    public void Analyze_DuplicateLinkedVisual_ProducesWarningForEachInput()
    {
        var service = new InputMapDiagnosticsService(new MameInputPortResolver());
        var visualId = Guid.NewGuid();
        var inputs = new[]
        {
            new InputDefinitionModel { Id = "a", Name = "A", Kind = InputDefinitionKind.Button, ButtonNumber = "1", LinkedVisualElementId = visualId },
            new InputDefinitionModel { Id = "b", Name = "B", Kind = InputDefinitionKind.Button, ButtonNumber = "2", LinkedVisualElementId = visualId }
        };

        var diagnostics = service.Analyze(FruitMachinePlatformType.MPU4, inputs);

        var duplicates = diagnostics.Where(d => d.Code == "input.duplicate_linked_visual").ToList();
        Assert.Equal(2, duplicates.Count);
        Assert.All(duplicates, d => Assert.Equal(InputMapDiagnosticSeverity.Warning, d.Severity));
        Assert.Contains(duplicates, d => d.InputDefinitionId == "a");
        Assert.Contains(duplicates, d => d.InputDefinitionId == "b");
    }
}
