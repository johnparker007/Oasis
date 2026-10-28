using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class UnsavedChangesCoordinatorTests
{
    [Theory]
    [InlineData(UnsavedChangesChoice.Cancel)]
    [InlineData(UnsavedChangesChoice.Discard)]
    public async Task CancelAndDiscardNeverSave(UnsavedChangesChoice choice)
    {
        var saves = 0;
        var prompted = false;
        var targets = new[] { Target("one", () => saves++), Target("two", () => saves++) };

        var result = await UnsavedChangesCoordinator.ResolveAsync("Close project", targets, request =>
        {
            prompted = true;
            Assert.Equal("Close project", request.Operation);
            Assert.Equal(new[] { "one", "two" }, request.Assets.Select(asset => asset.Id));
            return Task.FromResult(choice);
        });

        Assert.True(prompted);
        Assert.Equal(choice, result);
        Assert.Equal(0, saves);
    }

    [Fact]
    public async Task SaveStopsAtFirstFailureAndKeepsEarlierSavesCompleted()
    {
        var saved = new List<string>();
        var targets = new[]
        {
            Target("first", () => saved.Add("first")),
            new UnsavedChangesTarget(Asset("second"), () => throw new InvalidOperationException("save failed")),
            Target("third", () => saved.Add("third"))
        };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            UnsavedChangesCoordinator.ResolveAsync("Close project", targets,
                _ => Task.FromResult(UnsavedChangesChoice.Save)));

        Assert.Equal("save failed", error.Message);
        Assert.Equal(new[] { "first" }, saved);
    }

    [Fact]
    public async Task NoTargetsSkipPromptAndReturnSave()
    {
        var prompted = false;

        var result = await UnsavedChangesCoordinator.ResolveAsync("Close project",
            Array.Empty<UnsavedChangesTarget>(), _ =>
            {
                prompted = true;
                return Task.FromResult(UnsavedChangesChoice.Cancel);
            });

        Assert.Equal(UnsavedChangesChoice.Save, result);
        Assert.False(prompted);
    }

    private static UnsavedChangesTarget Target(string id, Action save) =>
        new(Asset(id), () => { save(); return Task.CompletedTask; });

    private static UnsavedAssetInfo Asset(string id) =>
        new(id, $"Asset {id}", "Panel", $"Assets/{id}.json", false);
}
