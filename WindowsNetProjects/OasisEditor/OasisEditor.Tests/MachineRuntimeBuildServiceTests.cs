using Xunit;
using System.Text.Json;
using OasisEditor.Features.CabinetEditor.Models;
using SkiaSharp;

namespace OasisEditor.Tests;

public sealed class MachineRuntimeBuildServiceTests
{
    [Fact]
    public void BuildFromCabinetDocument_WritesDeterministicVersionedBuildAndCopiesGlb()
    {
        var root = CreateTempRoot();
        var project = CreateProject(root);
        var cabinetDir = Directory.CreateDirectory(Path.Combine(project.AssetsDirectory, "Cabinet3D", "Test Cabinet")).FullName;
        var sourceGlb = Path.Combine(cabinetDir, "source.glb");
        File.WriteAllBytes(sourceGlb, [1, 2, 3]);
        File.WriteAllText(Path.Combine(cabinetDir, ProjectAssetPathService.Cabinet3DManifestFileName), CabinetDocumentStorage.Serialize(new CabinetDocument(1, new CabinetModelReference("source.glb", 2.5, "Z"), [], CabinetPreviewSettings.Default)));
        var stale = Path.Combine(project.GeneratedDirectory, "Builds", "Test Cabinet", "stale.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(stale)!);
        File.WriteAllText(stale, "stale");

        var result = new MachineRuntimeBuildService().BuildFromCabinetDocument(project, Path.Combine(cabinetDir, ProjectAssetPathService.Cabinet3DManifestFileName));

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(Path.Combine(project.GeneratedDirectory, "Builds", "Test Cabinet"), result.BuildRoot);
        Assert.False(File.Exists(stale));
        Assert.Equal([1, 2, 3], File.ReadAllBytes(Path.Combine(result.BuildRoot!, "cabinet", "cabinet.glb")));
        using var machine = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.BuildRoot, "machine.runtime.json")));
        Assert.Equal("oasis.machine.runtime", machine.RootElement.GetProperty("schema").GetString());
        Assert.Equal(3, machine.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Empty(machine.RootElement.GetProperty("faces").EnumerateArray());
        Assert.Equal("TestProject", machine.RootElement.GetProperty("machineId").GetString());
        Assert.Equal("cabinet/cabinet.runtime.json", machine.RootElement.GetProperty("cabinetManifest").GetString());
        using var cabinet = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.BuildRoot, "cabinet", "cabinet.runtime.json")));
        Assert.Equal("oasis.cabinet.runtime", cabinet.RootElement.GetProperty("schema").GetString());
        Assert.Equal(1, cabinet.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("Test Cabinet", cabinet.RootElement.GetProperty("cabinetId").GetString());
        Assert.Equal("cabinet.glb", cabinet.RootElement.GetProperty("glb").GetString());
        Assert.Equal(2.5, cabinet.RootElement.GetProperty("scale").GetDouble());
        Assert.Equal("Z", cabinet.RootElement.GetProperty("upAxis").GetString());
    }

    [Fact]
    public void BuildFromCabinetDocument_ExportsAssignedFacesIntoRuntimeBuild()
    {
        var root = CreateTempRoot();
        var project = CreateProject(root);
        var cabinetDir = Directory.CreateDirectory(Path.Combine(project.AssetsDirectory, "Cabinet3D", "Test Cabinet")).FullName;
        var sourceGlb = Path.Combine(cabinetDir, "source.glb");
        File.WriteAllBytes(sourceGlb, [1, 2, 3]);
        File.WriteAllText(Path.Combine(cabinetDir, ProjectAssetPathService.Cabinet3DManifestFileName), CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("source.glb").WithTargetOverride(new CabinetTargetOverride("target-front", " INVERTED ", 450, true))));
        var faceDir = Directory.CreateDirectory(Path.Combine(project.AssetsDirectory, "Faces", "Front Face")).FullName;
        WriteSolidPng(Path.Combine(faceDir, "artwork.png"), 4, 4, SKColors.Red);
        WriteSolidPng(Path.Combine(faceDir, "mask.png"), 4, 4, SKColors.White);
        var faceDocument = CreateFaceDocument("face-runtime", "target-front", "Assets/Faces/Front Face/artwork.png", "Assets/Faces/Front Face/mask.png");
        File.WriteAllText(Path.Combine(faceDir, ProjectAssetPathService.FaceManifestFileName), FaceDocumentStorage.Serialize(faceDocument));
        var backFaceDir = Directory.CreateDirectory(Path.Combine(project.AssetsDirectory, "Faces", "Back Face")).FullName;
        WriteSolidPng(Path.Combine(backFaceDir, "artwork.png"), 4, 4, SKColors.Blue);
        WriteSolidPng(Path.Combine(backFaceDir, "mask.png"), 4, 4, SKColors.White);
        var backFaceDocument = CreateFaceDocument("face-runtime-back", "target-back", "Assets/Faces/Back Face/artwork.png", "Assets/Faces/Back Face/mask.png");
        File.WriteAllText(Path.Combine(backFaceDir, ProjectAssetPathService.FaceManifestFileName), FaceDocumentStorage.Serialize(backFaceDocument));

        var result = new MachineRuntimeBuildService().BuildFromCabinetDocument(project, Path.Combine(cabinetDir, ProjectAssetPathService.Cabinet3DManifestFileName));

        Assert.True(result.Success, result.ErrorMessage);
        var faceBuildDirectory = Path.Combine(result.BuildRoot!, "faces", "Front Face");
        Assert.True(File.Exists(Path.Combine(faceBuildDirectory, "face.runtime.json")));
        Assert.True(File.Exists(Path.Combine(faceBuildDirectory, "artwork.png")));
        Assert.True(File.Exists(Path.Combine(faceBuildDirectory, "mask.png")));
        Assert.True(File.Exists(Path.Combine(faceBuildDirectory, "trayId.png")));
        Assert.True(File.Exists(Path.Combine(faceBuildDirectory, "lampIds0.png")));
        Assert.True(File.Exists(Path.Combine(faceBuildDirectory, "lampWeights0.png")));
        using var machine = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.BuildRoot, "machine.runtime.json")));
        var faces = machine.RootElement.GetProperty("faces").EnumerateArray().ToArray();
        Assert.Equal(2, faces.Length);
        var face = Assert.Single(faces, candidate => candidate.GetProperty("faceId").GetString() == "face-runtime");
        Assert.Equal("Front Face", face.GetProperty("assetName").GetString());
        Assert.Equal("target-front", face.GetProperty("cabinetFaceTargetId").GetString());
        Assert.Equal("inverted", face.GetProperty("frontSide").GetString());
        Assert.Equal(90, face.GetProperty("faceRotation").GetInt32());
        Assert.True(face.GetProperty("faceFlipHorizontal").GetBoolean());
        Assert.Equal("faces/Front Face/face.runtime.json", face.GetProperty("manifest").GetString());
        var normalFace = Assert.Single(faces, candidate => candidate.GetProperty("faceId").GetString() == "face-runtime-back");
        Assert.Equal("target-back", normalFace.GetProperty("cabinetFaceTargetId").GetString());
        Assert.Equal("normal", normalFace.GetProperty("frontSide").GetString());
        Assert.Equal(0, normalFace.GetProperty("faceRotation").GetInt32());
        Assert.False(normalFace.GetProperty("faceFlipHorizontal").GetBoolean());

        File.WriteAllText(Path.Combine(cabinetDir, ProjectAssetPathService.Cabinet3DManifestFileName), CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("source.glb").WithTargetOverride(new CabinetTargetOverride("target-front", CabinetTargetOverride.NormalFrontSide, 270, false))));
        var normalResult = new MachineRuntimeBuildService().BuildFromCabinetDocument(project, Path.Combine(cabinetDir, ProjectAssetPathService.Cabinet3DManifestFileName));
        Assert.True(normalResult.Success, normalResult.ErrorMessage);
        using var normalMachine = JsonDocument.Parse(File.ReadAllText(Path.Combine(normalResult.BuildRoot!, "machine.runtime.json")));
        var changedFace = Assert.Single(normalMachine.RootElement.GetProperty("faces").EnumerateArray(), candidate => candidate.GetProperty("faceId").GetString() == "face-runtime");
        Assert.Equal("normal", changedFace.GetProperty("frontSide").GetString());
        Assert.Equal(270, changedFace.GetProperty("faceRotation").GetInt32());
        Assert.False(changedFace.GetProperty("faceFlipHorizontal").GetBoolean());

        using var faceManifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(faceBuildDirectory, "face.runtime.json")));
        Assert.Equal(1, faceManifest.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("artwork.png", faceManifest.RootElement.GetProperty("artwork").GetString());
        Assert.Equal("mask.png", faceManifest.RootElement.GetProperty("mask").GetString());
    }


    [Fact]
    public void BuildFromCabinetDocument_UsesProvidedCabinetDocumentForUnsavedTargetOverride()
    {
        var root = CreateTempRoot();
        var project = CreateProject(root);
        var cabinetDir = Directory.CreateDirectory(Path.Combine(project.AssetsDirectory, "Cabinet3D", "Realistic Cabinet")).FullName;
        var sourceGlb = Path.Combine(cabinetDir, "source.glb");
        File.WriteAllBytes(sourceGlb, [1, 2, 3]);
        var manifestPath = Path.Combine(cabinetDir, ProjectAssetPathService.Cabinet3DManifestFileName);
        File.WriteAllText(manifestPath, CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("source.glb").WithTargetOverride(new CabinetTargetOverride("topGlass1", CabinetTargetOverride.NormalFrontSide))));
        var faceDir = Directory.CreateDirectory(Path.Combine(project.AssetsDirectory, "Faces", "Top Glass Face")).FullName;
        WriteSolidPng(Path.Combine(faceDir, "artwork.png"), 4, 4, SKColors.Red);
        WriteSolidPng(Path.Combine(faceDir, "mask.png"), 4, 4, SKColors.White);
        File.WriteAllText(Path.Combine(faceDir, ProjectAssetPathService.FaceManifestFileName), FaceDocumentStorage.Serialize(CreateFaceDocument("face-top-glass", "topGlass1", "Assets/Faces/Top Glass Face/artwork.png", "Assets/Faces/Top Glass Face/mask.png")));

        var service = new MachineRuntimeBuildService();
        var normalResult = service.BuildFromCabinetDocument(project, manifestPath);
        Assert.True(normalResult.Success, normalResult.ErrorMessage);
        using var normalMachine = JsonDocument.Parse(File.ReadAllText(Path.Combine(normalResult.BuildRoot!, "machine.runtime.json")));
        var normalFace = Assert.Single(normalMachine.RootElement.GetProperty("faces").EnumerateArray());
        Assert.Equal("normal", normalFace.GetProperty("frontSide").GetString());

        var unsavedCabinetDocument = CabinetDocument.FromModelPath("source.glb").WithTargetOverride(new CabinetTargetOverride("topGlass1", CabinetTargetOverride.InvertedFrontSide, 180, true));
        var invertedResult = service.BuildFromCabinetDocument(project, manifestPath, unsavedCabinetDocument);
        Assert.True(invertedResult.Success, invertedResult.ErrorMessage);
        using var invertedMachine = JsonDocument.Parse(File.ReadAllText(Path.Combine(invertedResult.BuildRoot!, "machine.runtime.json")));
        var invertedFace = Assert.Single(invertedMachine.RootElement.GetProperty("faces").EnumerateArray());
        Assert.Equal("inverted", invertedFace.GetProperty("frontSide").GetString());
        Assert.Equal(180, invertedFace.GetProperty("faceRotation").GetInt32());
        Assert.True(invertedFace.GetProperty("faceFlipHorizontal").GetBoolean());
    }

    [Fact]
    public void BuildFromCabinetDocument_AssignedTargetMismatch_ReturnsClearFailure()
    {
        var root = CreateTempRoot();
        var project = CreateProject(root);
        var cabinetDir = Directory.CreateDirectory(Path.Combine(project.AssetsDirectory, "Cabinet3D", "Mismatch Cabinet")).FullName;
        var sourceGlb = Path.Combine(cabinetDir, "source.glb");
        File.WriteAllBytes(sourceGlb, [1, 2, 3]);
        var manifestPath = Path.Combine(cabinetDir, ProjectAssetPathService.Cabinet3DManifestFileName);
        File.WriteAllText(manifestPath, CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("source.glb").WithTargetOverride(new CabinetTargetOverride("topGlass1", CabinetTargetOverride.InvertedFrontSide))));
        var faceDir = Directory.CreateDirectory(Path.Combine(project.AssetsDirectory, "Faces", "Mismatched Face")).FullName;
        WriteSolidPng(Path.Combine(faceDir, "artwork.png"), 4, 4, SKColors.Red);
        WriteSolidPng(Path.Combine(faceDir, "mask.png"), 4, 4, SKColors.White);
        File.WriteAllText(Path.Combine(faceDir, ProjectAssetPathService.FaceManifestFileName), FaceDocumentStorage.Serialize(CreateFaceDocument("face-mismatch", "OasisFace_Top-Glass 1", "Assets/Faces/Mismatched Face/artwork.png", "Assets/Faces/Mismatched Face/mask.png")));

        var result = new MachineRuntimeBuildService().BuildFromCabinetDocument(project, manifestPath);

        Assert.False(result.Success);
        Assert.Contains("no matching CabinetTargetOverride", result.ErrorMessage);
        Assert.Contains("face-mismatch", result.ErrorMessage);
        Assert.Contains("OasisFace_Top-Glass 1", result.ErrorMessage);
        Assert.Contains("topGlass1", result.ErrorMessage);
    }

    [Fact]
    public void BuildFromCabinetDocument_MissingGlb_ReturnsClearFailure()
    {
        var root = CreateTempRoot();
        var project = CreateProject(root);
        var cabinetDir = Directory.CreateDirectory(Path.Combine(project.AssetsDirectory, "Cabinet3D", "Broken Cabinet")).FullName;
        var manifest = Path.Combine(cabinetDir, ProjectAssetPathService.Cabinet3DManifestFileName);
        File.WriteAllText(manifest, CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("missing.glb")));

        var result = new MachineRuntimeBuildService().BuildFromCabinetDocument(project, manifest);

        Assert.False(result.Success);
        Assert.Contains("GLB model was not found", result.ErrorMessage);
    }

    private static FaceDocumentModel CreateFaceDocument(string faceId, string targetId, string artworkPath, string maskPath)
    {
        return new FaceDocumentModel
        {
            Id = faceId,
            Title = "Front Face",
            AssignedCabinetFaceTargetId = targetId,
            SourceRegion = new FaceSourceRegionModel { X = 0, Y = 0, Width = 4, Height = 4 },
            MaskLayer = new FaceMaskLayerModel { AssetPath = maskPath, Width = 4, Height = 4 },
            Elements =
            [
                new FaceArtworkElement
                {
                    ObjectId = "artwork",
                    Name = "Artwork",
                    X = 0,
                    Y = 0,
                    Width = 4,
                    Height = 4,
                    IsVisible = true,
                    AssetPath = artworkPath
                }
            ]
        };
    }

    private static void WriteSolidPng(string path, int width, int height, SKColor color)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
    }

    private static EditorProject CreateProject(string root)
    {
        var projectDir = Path.Combine(root, "TestProject");
        var assets = Directory.CreateDirectory(Path.Combine(projectDir, "Assets")).FullName;
        var machines = Directory.CreateDirectory(Path.Combine(projectDir, "Machines")).FullName;
        var generated = Directory.CreateDirectory(Path.Combine(projectDir, "Generated")).FullName;
        return new EditorProject { Name = "TestProject", ProjectFilePath = Path.Combine(projectDir, "TestProject.oasisproj"), ProjectDirectory = projectDir, AssetsDirectory = assets, MachinesDirectory = machines, GeneratedDirectory = generated };
    }

    private static string CreateTempRoot() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "OasisEditorTests", Guid.NewGuid().ToString("N"))).FullName;
}
