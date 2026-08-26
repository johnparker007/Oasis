using OasisEditor;
using OasisEditor.Automation;
using OasisEditor.Features.CabinetEditor.Models;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Tests;

public sealed class AssetPackageLayoutBugTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"OasisAssetPackageBugTests-{Guid.NewGuid():N}");

    [Fact]
    public void CreateProject_DoesNotCreateLegacyAssetPlaceholderFolders()
    {
        var projectDirectory = new ProjectScaffolder().CreateProject("PackageProject", _root);

        Assert.False(Directory.Exists(Path.Combine(projectDirectory, "Assets", "Audio")));
        Assert.False(Directory.Exists(Path.Combine(projectDirectory, "Assets", "Fonts")));
        Assert.False(Directory.Exists(Path.Combine(projectDirectory, "Assets", "Images")));
        Assert.True(Directory.Exists(Path.Combine(projectDirectory, "Assets", "Panel2D")));
        Assert.True(Directory.Exists(Path.Combine(projectDirectory, "Assets", "Faces")));
        Assert.True(Directory.Exists(Path.Combine(projectDirectory, "Assets", "Cabinet3D")));
    }

    [Fact]
    public void CreateProject_WritesOnlyCurrentProjectSchema()
    {
        var directory = new ProjectScaffolder().CreateProject("SchemaProject", _root);
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "SchemaProject.oasisproj")));
        Assert.Equal(EditorProject.CurrentSchemaVersion, document.RootElement.GetProperty("version").GetInt32());
    }

    [Theory]
    [InlineData("Assets/Panel2D/Main Panel/asset.panel2d", "Main Panel")]
    [InlineData("Assets/Faces/Top Glass/asset.face", "Top Glass")]
    [InlineData("Assets/Cabinet3D/Vogue/asset.cabinet3d", "Vogue")]
    public void CreateFromFile_ForPackageManifest_UsesEnclosingFolderAsTitle(string relativePath, string expectedTitle)
    {
        var path = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));

        var document = EditorDocument.CreateFromFile(path, "Opened.");

        Assert.Equal(expectedTitle, document.Title);
    }

    [Fact]
    public void BuildOpenDocumentData_ForPackageManifests_UsesEnclosingFolderAsTitle()
    {
        var panelJson = Panel2DDocumentStorage.Serialize("Internal Title", "Panel summary", [], []);
        var faceJson = FaceDocumentStorage.Serialize(FaceDocumentStorage.CreateEmpty("Internal Face"));
        var cabinetJson = CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("model.glb"));

        Assert.Equal("Main Panel", DocumentWorkspaceViewModel.BuildOpenDocumentData(Path.Combine(_root, "Assets", "Panel2D", "Main Panel", "asset.panel2d"), panelJson).PanelTitle);
        Assert.Equal("Top Glass", DocumentWorkspaceViewModel.BuildOpenDocumentData(Path.Combine(_root, "Assets", "Faces", "Top Glass", "asset.face"), faceJson).PanelTitle);
        Assert.Equal("Vogue", DocumentWorkspaceViewModel.BuildOpenDocumentData(Path.Combine(_root, "Assets", "Cabinet3D", "Vogue", "asset.cabinet3d"), cabinetJson).PanelTitle);
    }

    [Fact]
    public void SaveDocument_ForUnsavedGeneratedFace_CreatesFacePackageFilesAndUsesPackageTitle()
    {
        var project = CreateProject();
        var faceDocument = new FaceDocumentModel
        {
            Title = "Temporary Face",
            SourceRegion = new FaceSourceRegionModel { X = 0, Y = 0, Width = 2, Height = 2 },
            Trays = [new FaceTrayModel { ObjectId = "tray-zero", Bounds = new FaceSourceRegionModel { X = 0, Y = 0, Width = 2, Height = 2 } }],
            LampEmitters = [new FaceLampEmitterElement { ObjectId = "emitter-zero", TrayObjectId = "tray-zero", TrayId = 1, LampId = 0, CenterX = 1, CenterY = 1 }],
            Elements =
            [
                new FaceArtworkElement { ObjectId = "art", Name = "Artwork", Width = 2, Height = 2 }
            ]
        };
        var current = new DocumentTabViewModel(
            EditorDocument.CreateFaceStub("Temporary Face").MarkDirty(),
            faceDocumentJson: FaceDocumentStorage.Serialize(faceDocument));
        var savePath = Path.Combine(project.AssetsDirectory, "Faces", "Saved Face", "asset.face");

        var saved = new DocumentSaveService().SaveDocument(current, savePath, project);

        Assert.Equal("Saved Face", saved.Title);
        Assert.False(saved.Document.IsUntitled);
        Assert.True(File.Exists(Path.Combine(project.AssetsDirectory, "Faces", "Saved Face", "asset.face")));
        var artworkPath = Path.Combine(project.GeneratedDirectory, "Faces", "Saved Face", "Artwork", "artwork.png");
        Assert.True(File.Exists(artworkPath));
        Assert.True(File.Exists(FaceArtworkGeneratedPathService.GetBasePathFromOutput(artworkPath)));
        Assert.False(Directory.Exists(Path.Combine(project.AssetsDirectory, "Faces", "Saved Face", "generated")));
        Assert.True(File.Exists(Path.Combine(project.AssetsDirectory, "Faces", "Saved Face", "mask.png")));
        var savedFace = saved.GetFaceDocument();
        Assert.Equal(0, Assert.Single(savedFace.LampEmitters).LampId);
    }

    [Fact]
    public void SaveDocument_WhenDisposableRuntimePreviewExportFails_StillWritesAuthoredFace()
    {
        var project = CreateProject();
        var faceDocument = new FaceDocumentModel
        {
            Title = "Face With Unsupported Preview Lamp",
            SourceRegion = new FaceSourceRegionModel { X = 0, Y = 0, Width = 2, Height = 2 },
            Trays = [new FaceTrayModel { ObjectId = "tray", Bounds = new FaceSourceRegionModel { X = 0, Y = 0, Width = 2, Height = 2 } }],
            LampEmitters = [new FaceLampEmitterElement { ObjectId = "emitter", TrayObjectId = "tray", TrayId = 1, LampId = 255 }],
            Elements = [new FaceArtworkElement { ObjectId = "art", Name = "Artwork", Width = 2, Height = 2 }]
        };
        var current = new DocumentTabViewModel(EditorDocument.CreateFaceStub(faceDocument.Title).MarkDirty(), faceDocumentJson: FaceDocumentStorage.Serialize(faceDocument));
        var savePath = Path.Combine(project.AssetsDirectory, "Faces", "Saved Despite Preview Failure", "asset.face");

        var saved = new DocumentSaveService().SaveDocument(current, savePath, project);

        Assert.True(File.Exists(savePath));
        Assert.Equal(255, Assert.Single(saved.GetFaceDocument().LampEmitters).LampId);
        Assert.Null(saved.GetFaceDocument().RuntimeRenderAssets);
    }

    [Fact]
    public void SaveDocument_PreservesCompleteArtworkOverrideRecipeAndAuthoredAsset()
    {
        var project=CreateProject();
        var overridePath=Path.Combine(project.AssetsDirectory,"Faces","Source Face","ArtworkOverride","override.png");
        WriteSolidPng(overridePath,40,20,SKColors.Magenta);
        var registration=new FacePerspectiveRegistrationModel
        {
            TopLeft=new(){X=.1,Y=.2},TopRight=new(){X=.85,Y=.1},
            BottomRight=new(){X=.9,Y=.8},BottomLeft=new(){X=.15,Y=.9}
        };
        var model=new FaceDocumentModel
        {
            Title="Source Face",SourceRegion=new FaceSourceRegionModel{Width=4,Height=2},
            Artwork=new FaceArtworkModel
            {
                Source=new FaceArtworkSourceModel{Kind=FaceArtworkSourceKind.Image,AssetPath="Assets/source.png",PixelWidth=4,PixelHeight=2},
                Geometry=new FaceArtworkGeometryModel{PerspectiveRegistration=FacePerspectiveRegistrationModel.FullImage},
                ProcessingPipeline=new ImageProcessingPipelineModel(),OutputWidth=4,OutputHeight=2,
                FinalOutputWidth=4000,FinalOutputHeight=2000,
                Override=new FaceArtworkOverrideModel
                {
                    Enabled=false,AssetPath="Assets/Faces/Source Face/ArtworkOverride/override.png",PixelWidth=40,PixelHeight=20,
                    PerspectiveRegistration=registration,X=-.05,Y=.07,Width=1.1,Height=.93,ContentRevision=12
                }
            }
        };
        var current=new DocumentTabViewModel(EditorDocument.CreateFaceStub(model.Title).MarkDirty(),faceDocumentJson:FaceDocumentStorage.Serialize(model));
        var savePath=Path.Combine(project.AssetsDirectory,"Faces","Saved Override","asset.face");

        var saved=new DocumentSaveService().SaveDocument(current,savePath,project);
        Assert.True(FaceDocumentStorage.TryReadValidated(File.ReadAllText(savePath),out var file,out var error),error);
        var persisted=FaceDocumentStorage.ToModel(file);var artwork=Assert.IsType<FaceArtworkModel>(persisted.Artwork);
        var value=Assert.IsType<FaceArtworkOverrideModel>(artwork.Override);
        Assert.False(value.Enabled);Assert.Equal((40,20),(value.PixelWidth,value.PixelHeight));Assert.Equal(12,value.ContentRevision);
        Assert.Equal((-.05,.07,1.1,.93),(value.X,value.Y,value.Width,value.Height));
        Assert.Equal((.1,.2),(value.PerspectiveRegistration.TopLeft.X,value.PerspectiveRegistration.TopLeft.Y));
        Assert.Equal((.85,.1),(value.PerspectiveRegistration.TopRight.X,value.PerspectiveRegistration.TopRight.Y));
        Assert.Equal((.9,.8),(value.PerspectiveRegistration.BottomRight.X,value.PerspectiveRegistration.BottomRight.Y));
        Assert.Equal((.15,.9),(value.PerspectiveRegistration.BottomLeft.X,value.PerspectiveRegistration.BottomLeft.Y));
        Assert.Equal((4000,2000),(artwork.FinalOutputWidth,artwork.FinalOutputHeight));
        Assert.Equal("Assets/Faces/Source Face/ArtworkOverride/override.png",value.AssetPath);
        Assert.True(File.Exists(overridePath));using(var source=SKBitmap.Decode(overridePath))Assert.Equal(SKColors.Magenta,source.GetPixel(0,0));Assert.NotNull(saved.GetFaceDocument().Artwork?.Override);
    }


    [Fact]
    public void GenerateFaceFromSourceShape_WritesPendingArtworkWithSourcePixelsBeforeFirstSave()
    {
        var project = CreateProject();
        var sourceArtworkPath = Path.Combine(project.AssetsDirectory, "source.png");
        WriteSolidPng(sourceArtworkPath, 4, 4, SKColors.Red);
        var panel = new Panel2DDocumentModel
        {
            Elements =
            [
                new PanelElementModel
                {
                    ObjectId = "background",
                    Name = "Background",
                    Kind = PanelElementKind.Background,
                    X = 0,
                    Y = 0,
                    Width = 4,
                    Height = 4,
                    AssetPath = "Assets/source.png"
                }
            ]
        };
        var sourceShape = new PanelFaceSourceShapeModel
        {
            Id = "shape",
            Name = "Glass",
            TopLeft = new FacePointModel { X = 0, Y = 0 },
            TopRight = new FacePointModel { X = 4, Y = 0 },
            BottomRight = new FacePointModel { X = 4, Y = 4 },
            BottomLeft = new FacePointModel { X = 0, Y = 4 }
        };
        var pendingDirectory = Path.Combine(project.GeneratedDirectory, "Faces", "_unsaved", "pending-face");

        var result = new FaceGenerationService().GenerateFromPanelFaceSourceShape(
            panel,
            sourceShape,
            "Unsaved Face",
            projectDirectory: project.ProjectDirectory,
            generatedDirectory: project.GeneratedDirectory,
            faceAssetName: "Unsaved Face",
            faceAssetDirectory: pendingDirectory);

        var artwork = Assert.IsType<FaceArtworkElement>(Assert.Single(result.Document.Elements.OfType<FaceArtworkElement>()));
        var artworkPath = Path.Combine(project.ProjectDirectory, artwork.AssetPath!.Replace('/', Path.DirectorySeparatorChar));
        Assert.Equal("Generated/Faces/Unsaved Face/Artwork/artwork.png", artwork.AssetPath);
        Assert.True(File.Exists(artworkPath));
        Assert.True(File.Exists(FaceArtworkGeneratedPathService.GetBasePathFromOutput(artworkPath)));
        using var bitmap = SKBitmap.Decode(artworkPath);
        Assert.NotNull(bitmap);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(1, 1));
        Assert.False(Directory.Exists(Path.Combine(project.AssetsDirectory, "Faces", "Unsaved Face")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }


    private static void WriteSolidPng(string path, int width, int height, SKColor color)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    private EditorProject CreateProject()
    {
        Directory.CreateDirectory(_root);
        var assets = Path.Combine(_root, "Assets");
        var generated = Path.Combine(_root, "Generated");
        Directory.CreateDirectory(assets);
        Directory.CreateDirectory(generated);
        return new EditorProject
        {
            Name = "Test",
            ProjectFilePath = Path.Combine(_root, "Test.oasisproj"),
            ProjectDirectory = _root,
            AssetsDirectory = assets,
            MachinesDirectory = Path.Combine(_root, "Machines"),
            GeneratedDirectory = generated
        };
    }
}
