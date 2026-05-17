using Xunit;

namespace OasisEditor.Tests;

public sealed class InputMapDiagnosticsServiceLinkedVisualNoWarningTests
{
    [Fact]
    public void Analyze_UniqueLinkedVisuals_DoNotProduceDuplicateLinkedVisualWarnings()
    {
        var service = new InputMapDiagnosticsService(new MameInputPortResolver());
        var inputs = new[]
        {
            new InputDefinitionModel { Id = "a", Name = "A", Kind = InputDefinitionKind.Button, ButtonNumber = "1", LinkedVisualElementId = Guid.NewGuid() },
            new InputDefinitionModel { Id = "b", Name = "B", Kind = InputDefinitionKind.Button, ButtonNumber = "2", LinkedVisualElementId = Guid.NewGuid() }
        };

        var diagnostics = service.Analyze(FruitMachinePlatformType.MPU4, inputs);

        Assert.DoesNotContain(diagnostics, d => d.Code == "input.duplicate_linked_visual");
    }
}
