using System.Windows.Media;
using System.Windows.Media.Media3D;
using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.CabinetEditor.Services;
using OasisEditor.Features.CabinetEditor.ViewModels;
using Xunit;

namespace OasisEditor.Tests;

public sealed class CabinetViewerLifecycleTests
{
    [Fact]
    public void ContextCabinetMatchResolvesProjectAndLibraryScopes()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "Project");
        var libraryRoot = Path.Combine(Path.GetTempPath(), "Library");
        var project = new EditorProject { Name = "Project", ProjectDirectory = projectRoot, ProjectFilePath = Path.Combine(projectRoot, "Project.oasisproj"), AssetsDirectory = Path.Combine(projectRoot, "Assets"), GeneratedDirectory = Path.Combine(projectRoot, "Generated") };
        var projectManifest = Path.Combine(projectRoot, "Assets", "Cabinet3D", "Vogue", "asset.cabinet3d");
        var libraryManifest = Path.Combine(libraryRoot, "Cabinets", "Vogue", "asset.cabinet3d");
        Assert.True(CabinetModelDocumentViewModel.IsSelectedCabinet(project, libraryRoot, AssetReference.Project("Assets/Cabinet3D/Vogue/asset.cabinet3d"), projectManifest));
        Assert.True(CabinetModelDocumentViewModel.IsSelectedCabinet(project, libraryRoot, AssetReference.Library("Cabinets/Vogue/asset.cabinet3d"), libraryManifest));
        Assert.False(CabinetModelDocumentViewModel.IsSelectedCabinet(project, libraryRoot, AssetReference.Library("Cabinets/Rio/asset.cabinet3d"), libraryManifest));
    }

    [Theory]
    [InlineData("Assets/Cabinet3D/Vogue/asset.cabinet3d")]
    [InlineData("Library/Cabinets/Vogue/asset.cabinet3d")]
    public void ViewerResolvesPackageRelativeModelBesideOpenManifest(string relativeManifest)
    {
        var manifest = Path.Combine(Path.GetTempPath(), relativeManifest.Replace('/', Path.DirectorySeparatorChar));
        var tab = new DocumentTabViewModel(EditorDocument.CreateFromFile(manifest, "Cabinet", "Vogue"), cabinetDocumentJson: CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("vogue.glb")));
        using var viewer = new CabinetModelDocumentViewModel(new CountingLoader(CreateModel()), tab);
        Assert.Equal(Path.Combine(Path.GetDirectoryName(manifest)!, "vogue.glb"), viewer.ModelPath);
        Assert.Equal("vogue.glb", tab.GetCabinetDocument().Model.Path);
    }
    [Fact]
    public async Task CabinetViewer_IsCreatedOnce_LoadsOnce_AndFramesLoadedModel()
    {
        var document = CreateDocument();
        var loader = new CountingLoader(CreateModel());
        var viewer = new CabinetModelDocumentViewModel(loader, document);
        viewer.Initialize();

        await WaitUntilAsync(() => !viewer.IsLoading);

        Assert.Equal(1, loader.LoadCount);
        Assert.Same(loader.Model, viewer.Viewport.Model);
        Assert.Empty(viewer.ReflectionEditor.SurfaceChoices);
        Assert.False(viewer.Viewport.ModelBounds.IsEmpty);
        Assert.NotEqual(new Point3D(10, 6.5, 10), viewer.Viewport.CameraPosition);
        viewer.Dispose();
    }

    [Fact]
    public void ReplacingCabinetDocument_DisposesExistingLazyViewer()
    {
        var document = CreateDocument();
        var viewer = new CabinetModelDocumentViewModel(new CountingLoader(CreateModel()), document);
        viewer.Initialize();
        var field = typeof(DocumentTabViewModel).GetField("_cabinetViewer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(document, viewer);
        Assert.Same(viewer, document.ExistingCabinetViewer);

        var replacement = document.GetCabinetDocument() with { Model = new CabinetModelReference("replacement.glb", 1, "Y") };
        document.CabinetDocumentJson = CabinetDocumentStorage.Serialize(replacement);

        Assert.Null(document.ExistingCabinetViewer);
        Assert.Null(viewer!.Viewport.Model);
    }

    [Fact]
    public void PreviewStateAndMachineContext_DoNotDirtyOrMutateCabinet()
    {
        var document = CreateDocument();
        var original = document.GetCabinetDocument();
        var viewer = new CabinetModelDocumentViewModel(new CountingLoader(CreateModel()), document);
        var firstMachine = MachineTab("First");
        var secondMachine = MachineTab("Second");

        viewer.SelectedLampPreviewMode = CabinetLampPreviewMode.LampsAllOn;
        viewer.SetMachineCompositionContext(firstMachine);
        viewer.SetMachineCompositionContext(secondMachine);
        viewer.SetMachineCompositionContext(null);

        Assert.False(document.IsDirty);
        Assert.Equal(original, document.GetCabinetDocument());
        Assert.Equal("No Machine context", viewer.PreviewingMachine);
        Assert.Null(viewer.Viewport.FacePreviewModel);
        viewer.Dispose();
    }

    [Fact]
    public async Task FailedReload_ClearsDiscoveredReflectionGeometry_WithoutMutatingAuthoredReflections()
    {
        var sourceId = "OasisFace_TopGlass";
        var plane = new CabinetReflectionPlane(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), 1, 1);
        var reflection = new CabinetReflectionDefinition(
            "glass-reflection",
            "Cabinet/Glass",
            0,
            [new CabinetReflectionSource(sourceId, plane)],
            CabinetReflectionSettings.RoughPlastic);
        var cabinet = CabinetDocument.FromModelPath("cabinet.glb") with { Reflections = [reflection] };
        var document = CreateDocument(cabinet);
        var authoredReflection = Assert.Single(document.GetCabinetDocument().Reflections!);
        var faceTarget = new CabinetFaceTarget(sourceId, sourceId, "Top Glass", [], new Vector3D(), new Point3D(), true, null);
        var receiverTarget = new CabinetReflectionReceiverTarget(
            "Cabinet/Glass",
            "Glass",
            "GlassMesh",
            [new CabinetReflectionMaterialSlot(0, "Glass", "glTF PBR")]);
        var loader = new SequenceLoader(
            CabinetModelLoadResult.Success(CreateModel(), [faceTarget], [receiverTarget]),
            CabinetModelLoadResult.Failure("GLB unavailable"));
        var viewer = new CabinetModelDocumentViewModel(loader, document);

        await viewer.LoadAsync();
        Assert.Single(viewer.ReflectionEditor.Targets);
        Assert.Single(viewer.ReflectionEditor.FaceTargets);
        Assert.Contains(viewer.ReflectionEditor.SurfaceChoices, choice => choice.SourceSurfaceTargetId == sourceId && !choice.IsMissing);

        await viewer.LoadAsync();

        Assert.Empty(viewer.ReflectionEditor.Targets);
        Assert.Empty(viewer.ReflectionEditor.FaceTargets);
        var missingChoice = Assert.Single(viewer.ReflectionEditor.SurfaceChoices);
        Assert.True(missingChoice.IsMissing);
        Assert.Equal(sourceId, missingChoice.SourceSurfaceTargetId);
        Assert.Same(authoredReflection, Assert.Single(viewer.ReflectionEditor.Items));
        Assert.Same(authoredReflection, Assert.Single(document.GetCabinetDocument().Reflections!));
        Assert.False(document.IsDirty);
        viewer.Dispose();
    }

    private static DocumentTabViewModel CreateDocument(CabinetDocument? cabinet = null)
    {
        cabinet ??= CabinetDocument.FromModelPath("cabinet.glb");
        return new DocumentTabViewModel(EditorDocument.CreateCabinet3DStub("Cabinet"), cabinetDocumentJson: CabinetDocumentStorage.Serialize(cabinet));
    }

    private static DocumentTabViewModel MachineTab(string name) => new(
        EditorDocument.CreateMachineStub(name),
        machineDocumentJson: MachineDocumentStorage.Serialize(MachineDocument.Create(name)));

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

    private sealed class SequenceLoader(params CabinetModelLoadResult[] results) : ICabinetModelLoader
    {
        private int _index;

        public Task<CabinetModelLoadResult> LoadAsync(string modelPath, CancellationToken cancellationToken = default)
        {
            var result = results[Math.Min(_index, results.Length - 1)];
            _index++;
            return Task.FromResult(result);
        }
    }
}
