using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FabricSevenSegmentRuntimeTests
{
    [Theory]
    [InlineData(FruitMachinePlatformType.Impact)]
    [InlineData(FruitMachinePlatformType.MPU5)]
    public void AmberPlatformsInvertImportedAlphaReversedMeaning(FruitMachinePlatformType platform)
    {
        Assert.Equal(15, AlphaCellOrder.SourceIndexForCanonicalCell(0, 16, false, platform));
        Assert.Equal(0, AlphaCellOrder.SourceIndexForCanonicalCell(0, 16, true, platform));
        Assert.Equal(0, AlphaCellOrder.SourceIndexForCanonicalCell(15, 16, false, platform));
        Assert.Equal(15, AlphaCellOrder.SourceIndexForCanonicalCell(15, 16, true, platform));
    }

    [Fact]
    public void NonAmberPlatformRetainsImportedAlphaReversedMeaning()
    {
        Assert.Equal(0, AlphaCellOrder.SourceIndexForCanonicalCell(0, 16, false, FruitMachinePlatformType.MPU4));
        Assert.Equal(15, AlphaCellOrder.SourceIndexForCanonicalCell(0, 16, true, FruitMachinePlatformType.MPU4));
    }
    [Fact]
    public void Snapshot_AlphaAddressingAndMasksRemainUnchanged()
    {
        var backend = new FabricEmulationBackend("fabric", "amber");
        var changes = new List<MachineSegmentChangedEventArgs>();
        backend.SegmentChanged += (_, change) => changes.Add(change);

        backend.PublishSnapshot(new FabricMachineSnapshot(1, [], [],
        [
            new FabricCharacterDisplay("alpha.0", [1u], [0], 1f),
            new FabricCharacterDisplay("alpha.1", [2u], [0], 1f)
        ], []));

        Assert.Collection(changes,
            change =>
            {
                Assert.Equal(0, change.CellId);
                Assert.Equal(1, change.SegmentMask);
                Assert.Equal(SegmentOutputType.NativeAlpha, change.OutputType);
            },
            change =>
            {
                Assert.Equal(FabricAbi.CharacterCapacity, change.CellId);
                Assert.Equal(2, change.SegmentMask);
                Assert.Equal(SegmentOutputType.NativeAlpha, change.OutputType);
            });
    }

}
