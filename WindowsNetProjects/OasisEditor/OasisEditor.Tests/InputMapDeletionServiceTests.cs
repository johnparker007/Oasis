using Xunit;

namespace OasisEditor.Tests;

public sealed class InputMapDeletionServiceTests
{
    [Fact]
    public void DeleteSelected_DeletesOneRow()
    {
        var inputs = CreateInputs("one", "two", "three");

        var deleted = InputMapDeletionService.DeleteSelected(inputs, [inputs[1]]);

        Assert.Equal(1, deleted);
        Assert.Equal(["one", "three"], inputs.Select(input => input.Id));
    }

    [Fact]
    public void DeleteSelected_DeletesContiguousRowsAndPreservesRemainingOrder()
    {
        var inputs = CreateInputs("one", "two", "three", "four", "five");

        InputMapDeletionService.DeleteSelected(inputs, [inputs[1], inputs[2], inputs[3]]);

        Assert.Equal(["one", "five"], inputs.Select(input => input.Id));
    }

    [Fact]
    public void DeleteSelected_DeletesNonContiguousRowsAndPreservesRemainingOrder()
    {
        var inputs = CreateInputs("one", "two", "three", "four", "five");

        InputMapDeletionService.DeleteSelected(inputs, [inputs[3], inputs[1]]);

        Assert.Equal(["one", "three", "five"], inputs.Select(input => input.Id));
    }

    [Fact]
    public void DeleteSelected_DeletesAllRows()
    {
        var inputs = CreateInputs("one", "two", "three");

        InputMapDeletionService.DeleteSelected(inputs, inputs.ToArray());

        Assert.Empty(inputs);
    }

    [Fact]
    public void DeleteSelected_IgnoresRowsFromAnotherModel()
    {
        var inputs = CreateInputs("one", "two");

        var deleted = InputMapDeletionService.DeleteSelected(
            inputs,
            [new InputDefinitionModel { Id = "one" }]);

        Assert.Equal(0, deleted);
        Assert.Equal(["one", "two"], inputs.Select(input => input.Id));
    }

    private static List<InputDefinitionModel> CreateInputs(params string[] ids) =>
        ids.Select(id => new InputDefinitionModel { Id = id, Name = id }).ToList();
}
