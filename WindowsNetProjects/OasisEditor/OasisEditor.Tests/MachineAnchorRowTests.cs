using Xunit;

namespace OasisEditor.Tests;

public sealed class MachineAnchorRowTests
{
    [Fact]
    public void SuccessfulIdEditUpdatesMachineRowAndNotifies()
    {
        using var tab = Tab(Anchor("rackBall01"));
        var row = Assert.Single(tab.MachineAnchorRows);
        var notifications = Notifications(row);

        row.Id = "rackBall08";

        Assert.Equal("rackBall08", row.Id);
        Assert.Equal("rackBall08", Assert.Single(tab.GetMachineDocument().Anchors).Id);
        Assert.Contains(nameof(MachineAnchorRow.Id), notifications);
    }

    [Fact]
    public void IdNormalizationNotifiesAndDisplaysStoredValue()
    {
        using var tab = Tab(Anchor("rackBall01"));
        var row = Assert.Single(tab.MachineAnchorRows);
        var notifications = Notifications(row);

        row.Id = "  rackBall08  ";

        Assert.Equal("rackBall08", row.Id);
        Assert.Equal("rackBall08", Assert.Single(tab.GetMachineDocument().Anchors).Id);
        Assert.Contains(nameof(MachineAnchorRow.Id), notifications);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad id")]
    [InlineData("a:b")]
    public void InvalidIdSnapsBackToAuthoritativeValue(string attemptedId)
    {
        using var tab = Tab(Anchor("rackBall01"));
        var row = Assert.Single(tab.MachineAnchorRows);
        var notifications = Notifications(row);

        row.Id = attemptedId;

        Assert.Equal("rackBall01", row.Id);
        Assert.Equal("rackBall01", Assert.Single(tab.GetMachineDocument().Anchors).Id);
        Assert.Contains(nameof(MachineAnchorRow.Id), notifications);
    }

    [Fact]
    public void DuplicateIdSnapsBackToAuthoritativeValue()
    {
        using var tab = Tab(Anchor("rackBall01"), Anchor("rackBall08"));
        var row = tab.MachineAnchorRows[1];
        var notifications = Notifications(row);

        row.Id = "rackBall01";

        Assert.Equal("rackBall08", row.Id);
        Assert.Equal(["rackBall01", "rackBall08"], tab.GetMachineDocument().Anchors.Select(anchor => anchor.Id).ToArray());
        Assert.Contains(nameof(MachineAnchorRow.Id), notifications);
    }

    [Fact]
    public void DisplayNameNormalizationNotifiesAndDisplaysStoredValue()
    {
        using var tab = Tab(Anchor("rackBall01"));
        var row = Assert.Single(tab.MachineAnchorRows);
        var notifications = Notifications(row);

        row.DisplayName = "  Rack Ball One  ";

        Assert.Equal("Rack Ball One", row.DisplayName);
        Assert.Equal("Rack Ball One", Assert.Single(tab.GetMachineDocument().Anchors).DisplayName);
        Assert.Contains(nameof(MachineAnchorRow.DisplayName), notifications);
    }

    [Fact]
    public void PositionAndRotationEditsUpdateRowMachineAndNotifyAllComponents()
    {
        using var tab = Tab(Anchor("rackBall01"));
        var row = Assert.Single(tab.MachineAnchorRows);
        var notifications = Notifications(row);

        row.PositionX = 1.25;
        row.RotationY = 90;

        Assert.Equal(1.25, row.PositionX);
        Assert.Equal(90, row.RotationY);
        var anchor = Assert.Single(tab.GetMachineDocument().Anchors);
        Assert.Equal(1.25, anchor.Position.X);
        Assert.Equal(90, anchor.Rotation.Y);
        Assert.Contains(nameof(MachineAnchorRow.PositionX), notifications);
        Assert.Contains(nameof(MachineAnchorRow.PositionY), notifications);
        Assert.Contains(nameof(MachineAnchorRow.PositionZ), notifications);
        Assert.Contains(nameof(MachineAnchorRow.RotationX), notifications);
        Assert.Contains(nameof(MachineAnchorRow.RotationY), notifications);
        Assert.Contains(nameof(MachineAnchorRow.RotationZ), notifications);
    }

    [Fact]
    public void UndoRedoRebuildsRowsFromAuthoritativeMachineValue()
    {
        using var tab = Tab(Anchor("rackBall01"));
        tab.MachineAnchorRows.Single().PositionZ = 3.5;
        Assert.Equal(3.5, tab.MachineAnchorRows.Single().PositionZ);

        Assert.True(tab.CommandService.TryUndo());
        Assert.Equal(0, tab.GetMachineDocument().Anchors.Single().Position.Z);
        Assert.Equal(0, tab.MachineAnchorRows.Single().PositionZ);

        Assert.True(tab.CommandService.TryRedo());
        Assert.Equal(3.5, tab.GetMachineDocument().Anchors.Single().Position.Z);
        Assert.Equal(3.5, tab.MachineAnchorRows.Single().PositionZ);
    }

    private static MachineAnchor Anchor(string id) => MachineAnchor.Create(id, id);
    private static DocumentTabViewModel Tab(params MachineAnchor[] anchors) => new(
        EditorDocument.CreateFromFile("C:/Project/Assets/Machines/Pool/asset.machine", "Machine"),
        machineDocumentJson: MachineDocumentStorage.Serialize(MachineDocument.Create("Pool") with { Anchors = anchors }));

    private static List<string?> Notifications(MachineAnchorRow row)
    {
        var notifications = new List<string?>();
        row.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        return notifications;
    }
}
