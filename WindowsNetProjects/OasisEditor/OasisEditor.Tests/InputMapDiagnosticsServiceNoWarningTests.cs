using Xunit;

namespace OasisEditor.Tests;

public sealed class InputMapDiagnosticsServiceNoWarningTests
{
    [Fact]
    public void Analyze_ValidDistinctMappedInputs_ProducesNoWarnings()
    {
        var service = new InputMapDiagnosticsService(new MameInputPortResolver());
        var inputs = new[]
        {
            new InputDefinitionModel { Id = "a", Name = "A", Kind = InputDefinitionKind.Button, ButtonNumber = "1", KeyboardShortcut = "Space" },
            new InputDefinitionModel { Id = "b", Name = "B", Kind = InputDefinitionKind.Button, ButtonNumber = "2", KeyboardShortcut = "Enter" },
            new InputDefinitionModel { Id = "c", Name = "Coin", Kind = InputDefinitionKind.Coin, CoinInput = true, KeyboardShortcut = "C" }
        };

        var diagnostics = service.Analyze(FruitMachinePlatformType.MPU4, inputs);

        Assert.Empty(diagnostics);
    }
}
