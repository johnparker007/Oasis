using Xunit;

namespace OasisEditor.Tests;

public sealed class InputMapDiagnosticsServiceInvalidEntryTests
{
    [Fact]
    public void Analyze_EntriesWithoutIds_AreIgnored()
    {
        var service = new InputMapDiagnosticsService(new MameInputPortResolver());
        var sharedVisualId = Guid.NewGuid();
        var inputs = new[]
        {
            new InputDefinitionModel { Id = "a", Name = "A", Kind = InputDefinitionKind.Button, ButtonNumber = "1", KeyboardShortcut = "Space", LinkedVisualElementId = sharedVisualId },
            new InputDefinitionModel { Id = "", Name = "Invalid", Kind = InputDefinitionKind.Button, ButtonNumber = "ABC", KeyboardShortcut = "Space", LinkedVisualElementId = sharedVisualId }
        };

        var diagnostics = service.Analyze(FruitMachinePlatformType.MPU4, inputs);

        Assert.Empty(diagnostics.Where(d => d.InputDefinitionId == string.Empty));
        Assert.DoesNotContain(diagnostics, d => d.Code == "input.duplicate_shortcut");
        Assert.DoesNotContain(diagnostics, d => d.Code == "input.duplicate_linked_visual");
    }
}
