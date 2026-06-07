using SkiaSharp;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceMaskLayerSystemTests : IDisposable
{
    private readonly string _projectDirectory;

    public FaceMaskLayerSystemTests()
    {
        _projectDirectory = Path.Combine(Path.GetTempPath(), $"OasisFaceMaskSystemTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_projectDirectory, "Generated", "Faces"));
        Directory.CreateDirectory(Path.Combine(_projectDirectory, "Assets"));
    }

    [Fact]
    public void FaceHierarchyProvider_ExposesMaskLayerAsSelectableLayer()
    {
        var document = new DocumentTabViewModel(
            EditorDocument.CreateFaceStub("Face"),
            faceDocumentJson: FaceDocumentStorage.Serialize(new FaceDocumentModel
            {
                Title = "Face",
                MaskLayer = CreateMaskLayer("Generated/Faces/mask.png", width: 320, height: 240)
            }));

        var groups = new FaceHierarchyProvider().Build(document);

        var layersGroup = Assert.Single(groups.Where(group => group.NodeKey == "group:layers"));
        var maskItem = Assert.Single(layersGroup.Children);
        Assert.Contains("Face Mask", maskItem.DisplayName);
        Assert.Contains("320×240", maskItem.DisplayName);
        Assert.NotNull(maskItem.PanelSelection);
        Assert.True(FaceMaskLayerSelectionService.IsMaskLayerSelection(maskItem.PanelSelection!.Value));
    }

    [Fact]
    public void Validate_ReportsMaskLayerAssetDimensionAndContributionDiagnostics()
    {
        WriteSolidPng(Path.Combine(_projectDirectory, "Generated", "Faces", "mask.png"), 8, 8, SKColors.White);
        var faceDocument = new FaceDocumentModel
        {
            Title = "Face",
            SourceRegion = new FaceSourceRegionModel { X = 0, Y = 0, Width = 10, Height = 10 },
            MaskLayer = new FaceMaskLayerModel
            {
                Id = "face-mask-layer",
                Name = "Face Mask",
                AssetPath = "Generated/Faces/mask.png",
                SourceRegion = new FaceSourceRegionModel { X = 0, Y = 0, Width = 10, Height = 10 },
                Width = 10,
                Height = 10,
                Contributions = []
            }
        };
        var project = CreateProject();

        var diagnostics = new FaceValidationService().Validate(faceDocument, project, []);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "Face.MaskLayer.Dimensions.AssetMismatch");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "Face.MaskLayer.Contributions.Missing");
    }

    [Fact]
    public void Validate_ReportsMissingMaskLayerAsset()
    {
        var faceDocument = new FaceDocumentModel
        {
            Title = "Face",
            MaskLayer = CreateMaskLayer("Generated/Faces/missing-mask.png", width: 10, height: 10)
        };
        var project = CreateProject();

        var diagnostics = new FaceValidationService().Validate(faceDocument, project, []);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "Face.MaskLayer.Asset.Missing");
    }

    public void Dispose()
    {
        if (Directory.Exists(_projectDirectory))
        {
            Directory.Delete(_projectDirectory, recursive: true);
        }
    }

    private EditorProject CreateProject()
    {
        return new EditorProject
        {
            Name = "Project",
            ProjectFilePath = Path.Combine(_projectDirectory, "Project.oasisproj"),
            ProjectDirectory = _projectDirectory,
            AssetsDirectory = Path.Combine(_projectDirectory, "Assets"),
            MachinesDirectory = Path.Combine(_projectDirectory, "Machines"),
            GeneratedDirectory = Path.Combine(_projectDirectory, "Generated")
        };
    }

    private static FaceMaskLayerModel CreateMaskLayer(string assetPath, int width, int height)
    {
        return new FaceMaskLayerModel
        {
            Id = "face-mask-layer",
            Name = "Face Mask",
            AssetPath = assetPath,
            SourceRegion = new FaceSourceRegionModel { X = 0, Y = 0, Width = width, Height = height },
            ExtractionThreshold = 24,
            GeneratedUtc = new DateTime(2026, 6, 7, 0, 0, 0, DateTimeKind.Utc),
            Width = width,
            Height = height,
            Contributions =
            [
                new FaceMaskContributionModel
                {
                    SourcePanel2DElementId = "lamp-1",
                    LinkedMachineObjectReference = MachineObjectReference.Lamp(1),
                    Bounds = new FaceSourceRegionModel { X = 1, Y = 1, Width = 2, Height = 2 },
                    PixelCount = 4
                }
            ]
        };
    }

    private static void WriteSolidPng(string path, int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
    }
}
