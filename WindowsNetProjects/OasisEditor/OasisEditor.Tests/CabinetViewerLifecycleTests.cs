using System.Windows.Media.Media3D;
using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.CabinetEditor.Services;
using OasisEditor.Features.CabinetEditor.ViewModels;
using Xunit;

namespace OasisEditor.Tests;

public sealed class CabinetViewerLifecycleTests
{
    [Fact]
    public async Task CabinetViewer_IsCreatedOnce_LoadsOnce_AndFramesLoadedModel()
    {
        var document = CreateDocument();
        var loader = new CountingLoader(CreateModel());
        var viewer = new CabinetModelDocumentViewModel(loader, document);

        await WaitUntilAsync(() => !viewer.IsLoading);

        Assert.Equal(1, loader.LoadCount);
        Assert.Same(loader.Model, viewer.Viewport.Model);
        Assert.False(viewer.Viewport.ModelBounds.IsEmpty);
        Assert.NotEqual(new Point3D(10, 6.5, 10), viewer.Viewport.CameraPosition);
        viewer.Dispose();
    }

    [Fact]
    public void ReplacingCabinetDocument_DisposesExistingLazyViewer()
    {
        var document = CreateDocument();
        var viewer = new CabinetModelDocumentViewModel(new CountingLoader(CreateModel()), document);
        var field = typeof(DocumentTabViewModel).GetField("_cabinetViewer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(document, viewer);
        Assert.Same(viewer, document.ExistingCabinetViewer);

        var replacement = document.GetCabinetDocument() with { Preview = new CabinetPreviewSettings(false, false) };
        document.CabinetDocumentJson = CabinetDocumentStorage.Serialize(replacement);

        Assert.Null(document.ExistingCabinetViewer);
        Assert.Null(viewer!.Viewport.Model);
    }

    private static DocumentTabViewModel CreateDocument()
    {
        var cabinet = CabinetDocument.FromModelPath("cabinet.glb");
        return new DocumentTabViewModel(EditorDocument.CreateCabinet3DStub("Cabinet"), cabinetDocumentJson: CabinetDocumentStorage.Serialize(cabinet));
    }

    private static Model3DGroup CreateModel()
    {
        var mesh = new MeshGeometry3D { Positions = new Point3DCollection { new(20, 30, 40), new(24, 30, 40), new(20, 36, 48) }, TriangleIndices = new Int32Collection { 0, 1, 2 } };
        return new Model3DGroup { Children = new Model3DCollection { new GeometryModel3D(mesh, null) } };
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++) await Task.Delay(10);
        Assert.True(condition());
    }

    private sealed class CountingLoader : ICabinetModelLoader
    {
        public CountingLoader(Model3DGroup model) => Model = model;
        public int LoadCount { get; private set; }
        public Model3DGroup Model { get; }
        public Task<CabinetModelLoadResult> LoadAsync(string modelPath, CancellationToken cancellationToken = default)
        {
            LoadCount++;
            return Task.FromResult(CabinetModelLoadResult.Success(Model));
        }
    }
}
