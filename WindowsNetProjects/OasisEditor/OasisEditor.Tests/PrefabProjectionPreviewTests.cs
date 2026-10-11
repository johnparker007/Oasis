using System.Windows.Media;
using System.Windows.Media.Media3D;
using OasisEditor.Features.CabinetEditor.Services;
using Xunit;

namespace OasisEditor.Tests;

public sealed class PrefabProjectionPreviewTests
{
    [Fact]
    public async Task SourcePreviewCachesImportsAndSelectsAuthoredOwnerWithoutCreatingGlbNodes()
    {
        var tab = new DocumentTabViewModel(EditorDocument.CreateObject3DStub("Prop"));
        tab.SetObject3DModelSource(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "prefab-preview.glb"));
        var loader = new FakeLoader();
        using var preview = new PrefabPreviewViewModel(tab, loader);
        await preview.RefreshTask;
        Assert.Equal(1, loader.LoadCount);
        var root = tab.GetObject3DDocument().Root.Id;
        var visual = Assert.IsType<Model3DGroup>(Assert.Single(preview.Visual.Children));
        var rendering = Assert.IsType<Model3DGroup>(Assert.Single(visual.Children));
        var geometry = Assert.IsType<GeometryModel3D>(Assert.Single(rendering.Children));
        Assert.True(preview.SelectGeometry(geometry));
        Assert.Equal(new EditorSelectionItem(EditorSelectionDomain.PrefabNode, root), tab.SelectionState.PrimaryItem);
        Assert.Empty(tab.GetObject3DDocument().Root.Children);
        tab.PrefabPositionX = 3;
        preview.Refresh();
        await preview.RefreshTask;
        Assert.Equal(1, loader.LoadCount);
        Assert.Equal(3, preview.SelectedBounds.X);
        tab.PrefabNodeName = "Renamed";
        preview.Refresh(); await preview.RefreshTask;
        Assert.Equal(1, loader.LoadCount);
    }

    [Fact]
    public void SharedHierarchyIncludesOnlyAuthoredNodesAndPreservesRowsOnEdits()
    {
        var tab = new DocumentTabViewModel(EditorDocument.CreateObject3DStub("Prop"));
        var hierarchy = new HierarchyViewModel(() => tab, [new PrefabHierarchyProvider()]);
        hierarchy.Refresh();
        var rootRow = Assert.Single(hierarchy.Items);
        tab.AddPrefabNode();
        var childId = tab.PrefabNodeId;
        hierarchy.Refresh();
        Assert.Same(rootRow, Assert.Single(hierarchy.Items));
        var child = rootRow.Children.Single(n => n.SelectionItem?.ObjectId == childId);
        Assert.True(child.IsSelected);
        tab.PrefabNodeName = "Group";
        hierarchy.Refresh();
        Assert.Same(child, rootRow.Children.Single(n => n.SelectionItem?.ObjectId == childId));
        Assert.Equal("Group", child.DisplayName);
        Assert.Single(child.Children); // Mandatory Transform entry, with no imported render nodes.
        tab.CommandService.TryUndo(); hierarchy.Refresh();
        Assert.Equal("Node", child.DisplayName);
    }

    [Theory]
    [InlineData("X", 0, 2, 0)]
    [InlineData("Y", 2, 0, 0)]
    [InlineData("Z", 0, 2, 0)]
    public void IntrinsicScaleAndUpAxisStayBeneathAuthoredNodeTransform(string upAxis, double x, double y, double z)
    {
        var source = upAxis == "Z" ? new Point3D(0,0,1) : new Point3D(1,0,0);
        var intrinsic = Features.MachineComposition.ViewModels.MachineCompositionTransform.Create(MachineObjectTransform.Identity, new("model.glb", 2, upAxis));
        var corrected = intrinsic.Transform(source);
        Assert.Equal(x, corrected.X, 10);
        Assert.Equal(y, corrected.Y, 10);
        Assert.Equal(z, corrected.Z, 10);
    }

    private sealed class FakeLoader : ICabinetModelLoader
    {
        public int LoadCount { get; private set; }
        public Task<CabinetModelLoadResult> LoadAsync(string path, CancellationToken cancellationToken = default)
        {
            LoadCount++;
            var mesh = new MeshGeometry3D { Positions = new Point3DCollection([new(0,0,0),new(1,0,0),new(0,1,0)]), TriangleIndices = new Int32Collection([0,1,2]) };
            var model = new Model3DGroup();
            model.Children.Add(new GeometryModel3D(mesh, new DiffuseMaterial(new SolidColorBrush(Colors.Gray))));
            model.Freeze();
            return Task.FromResult(CabinetModelLoadResult.Success(model));
        }
    }
}
