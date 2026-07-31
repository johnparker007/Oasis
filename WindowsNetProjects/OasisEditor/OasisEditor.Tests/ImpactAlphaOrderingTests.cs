using MfmeFmlDecoder.src.Model;
using MfmeFmlDecoder.src.Model.Component;
using OasisEditor.Features.FmlImport;
using Xunit;

namespace OasisEditor.Tests;

public sealed class ImpactAlphaOrderingTests
{
    private static readonly uint[] FabricCharacters = Enumerable.Range(1, 16).Select(value => (uint)value).ToArray();

    [Fact]
    public void FabricBackend_PublishesCharacterPositionsInArrayOrder()
    {
        using var backend = new FabricEmulationBackend("unused-runtime", "unused-amber");
        var updates = new List<(int CellId, int Mask)>();
        backend.SegmentChanged += (_, update) => updates.Add((update.CellId, update.SegmentMask));

        backend.PublishSnapshot(CreateSnapshot());

        Assert.Equal(Enumerable.Range(0, 16), updates.Select(update => update.CellId));
        Assert.Equal(FabricCharacters.Select(value => (int)value), updates.Select(update => update.Mask));
    }

    [Fact]
    public void ImportedReversedImpactAlpha_FabricUpdateReachesCanonicalStateInMfmeVisibleOrder()
    {
        var imported = ImportAlpha(reversed: true);
        var (document, dispatches, backend) = CreatePipeline(imported);
        using (backend)
        {
            backend.PublishSnapshot(CreateSnapshot());
            Assert.Single(dispatches)();
        }

        Assert.Equal(Enumerable.Range(1, 16), document.RuntimeState.GetSegmentCellMasks(imported.ObjectId, 16));
    }

    [Fact]
    public void NonReversedImpactAlpha_UsesOppositeSourceAddressDirection()
    {
        var imported = ImportAlpha(reversed: false);
        var (document, dispatches, backend) = CreatePipeline(imported);
        using (backend)
        {
            backend.PublishSnapshot(CreateSnapshot());
            Assert.Single(dispatches)();
        }

        Assert.Equal(Enumerable.Range(1, 16).Reverse(), document.RuntimeState.GetSegmentCellMasks(imported.ObjectId, 16));
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 15)]
    public void ReversedFlag_IsAppliedOnceWhileCanonicalizingBackendState(bool reversed, int firstSourceIndex)
    {
        Assert.Equal(firstSourceIndex, AlphaCellOrder.SourceIndexForCanonicalCell(0, 16, reversed, FruitMachinePlatformType.Impact));
    }

    private static PanelElementModel ImportAlpha(bool reversed)
    {
        var alpha = new Alpha { X = 0, Y = 0, Width = 160, Height = 20 };
        alpha.Booleans["Reversed"] = reversed;
        var imported = Assert.Single(new FmlToOasisMapper()
            .Map(new Layout([alpha]), new Dictionary<FmlDecodedImageKey, string>()).Elements);
        Assert.Equal(reversed, imported.IsReversed);
        return imported;
    }

    private static (DocumentTabViewModel Document, List<Action> Dispatches, FabricEmulationBackend Backend) CreatePipeline(PanelElementModel alpha)
    {
        var document = new DocumentTabViewModel(EditorDocument.CreateFromFile("panel.panel2d", "panel", "panel"));
        document.SetPanelElements([alpha]);
        var dispatches = new List<Action>();
        var adapter = new MachineSegmentRuntimeAdapter(() => [document], dispatches.Add, () => FruitMachinePlatformType.Impact);
        var backend = new FabricEmulationBackend("unused-runtime", "unused-amber");
        backend.SegmentChanged += (_, update) => adapter.ApplySegmentState(update.CellId, update.SegmentMask, update.OutputType);
        return (document, dispatches, backend);
    }

    private static FabricMachineSnapshot CreateSnapshot() => new(
        1,
        [],
        [],
        [new FabricCharacterDisplay("impact-alpha", FabricCharacters, new byte[16])],
        []);
}
