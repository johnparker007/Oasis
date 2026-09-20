using Xunit;
using OasisEditor.Progress;
using System.Text.Json;
using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.MachineEditor.Models;
using SkiaSharp;

namespace OasisEditor.Tests;

public sealed class MachineRuntimeBuildServiceTests
{
    [Fact]
    public void BuildFromMachineDocument_ExportsReflectionDefinitionAndVisibilityMask()
    {
        var root = CreateTempRoot();
        var project = CreateProject(root);
        var cabinetDir = Directory.CreateDirectory(Path.Combine(project.AssetsDirectory, "Cabinet3D", "Reflective")).FullName;
        WriteMinimalGlb(Path.Combine(cabinetDir, "source.glb"));
        WriteSolidPng(Path.Combine(cabinetDir, "side-mask.png"), 2, 2, SKColors.White);
        var reflection = new CabinetReflectionDefinition("side", "SideMesh", 0, [new CabinetReflectionSource("lowerGlass", new CabinetReflectionPlane(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), 2, 1))], CabinetReflectionSettings.RoughPlastic with { Enabled = false }, "side-mask.png");
        var document = CabinetDocument.FromModelPath("source.glb") with { Reflections = [reflection] };
        var cabinetManifestPath = Path.Combine(cabinetDir, ProjectAssetPathService.Cabinet3DManifestFileName);
        File.WriteAllText(cabinetManifestPath, CabinetDocumentStorage.Serialize(document));
        var machineManifestPath = CreateMachineAsset(project, "Reflective Machine", ToCabinetAssetPath(cabinetManifestPath), MachineDocumentExtensions.Empty("Reflective Machine"));

        var result = new MachineRuntimeBuildService().BuildFromMachineDocument(project, machineManifestPath, LoadMachine(machineManifestPath), NoOpEditorProgressReporter.Instance, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.BuildRoot!, "cabinet", "cabinet.runtime.json")));
        var exported = Assert.Single(json.RootElement.GetProperty("reflections").EnumerateArray());
        Assert.Equal("side", exported.GetProperty("id").GetString());
        Assert.Equal("SideMesh", exported.GetProperty("targetId").GetString());
        Assert.Equal("reflection-masks/side.png", exported.GetProperty("visibilityMask").GetString());
        Assert.True(File.Exists(Path.Combine(result.BuildRoot, "cabinet", "reflection-masks", "side.png")));
    }

    [Fact]
    public void BuildFromMachineDocument_WritesDeterministicVersionedBuildAndCopiesGlb()
    {
        var root = CreateTempRoot();
        var project = CreateProject(root);
        var cabinetDir = Directory.CreateDirectory(Path.Combine(project.AssetsDirectory, "Cabinet3D", "Test Cabinet")).FullName;
        var sourceGlb = Path.Combine(cabinetDir, "source.glb");
        WriteMinimalGlb(sourceGlb);
        var cabinetManifestPath = Path.Combine(cabinetDir, ProjectAssetPathService.Cabinet3DManifestFileName);
        File.WriteAllText(cabinetManifestPath, CabinetDocumentStorage.Serialize(new CabinetDocument(7, new CabinetModelReference("source.glb", 2.5, "Z"), [], CabinetPreviewSettings.Default)));
        var machineManifestPath = CreateMachineAsset(project, "Test Machine", ToCabinetAssetPath(cabinetManifestPath), MachineDocumentExtensions.Empty("Test Machine"));
        var stale = Path.Combine(project.GeneratedDirectory, "Builds", "Test Machine", "stale.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(stale)!);
        File.WriteAllText(stale, "stale");

        var result = new MachineRuntimeBuildService().BuildFromMachineDocument(project, machineManifestPath, LoadMachine(machineManifestPath), NoOpEditorProgressReporter.Instance, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(Path.Combine(project.GeneratedDirectory, "Builds", "Test Machine"), result.BuildRoot);
        Assert.False(File.Exists(stale));
        Assert.Equal(File.ReadAllBytes(sourceGlb), File.ReadAllBytes(Path.Combine(result.BuildRoot!, "cabinet", "cabinet.glb")));
        using var machine = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.BuildRoot, "machine.runtime.json")));
        Assert.Equal("oasis.machine.runtime", machine.RootElement.GetProperty("schema").GetString());
        Assert.Equal(4, machine.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Empty(machine.RootElement.GetProperty("faces").EnumerateArray());
        Assert.NotNull(machine.RootElement.GetProperty("machineId").GetString());
        Assert.Equal("Test Machine", machine.RootElement.GetProperty("displayName").GetString());
        Assert.Equal("cabinet/cabinet.runtime.json", machine.RootElement.GetProperty("cabinetManifest").GetString());
        Assert.Equal("Emulation", machine.RootElement.GetProperty("runtime").GetProperty("kind").GetString());
        using var cabinet = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.BuildRoot, "cabinet", "cabinet.runtime.json")));
        Assert.Equal("oasis.cabinet.runtime", cabinet.RootElement.GetProperty("schema").GetString());
        Assert.Equal(4, cabinet.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("Test Cabinet", cabinet.RootElement.GetProperty("cabinetId").GetString());
        Assert.Equal("cabinet.glb", cabinet.RootElement.GetProperty("glb").GetString());
        Assert.Equal(2.5, cabinet.RootElement.GetProperty("scale").GetDouble());
        Assert.Equal("Z", cabinet.RootElement.GetProperty("upAxis").GetString());
        Assert.Empty(cabinet.RootElement.GetProperty("reflections").EnumerateArray());
    }

    [Fact]
    public void BuildFromMachineDocument_ExportsAssignedFacesIntoRuntimeBuild()
    {
        var root = CreateTempRoot();
        var project = CreateProject(root);
        var cabinetDir = Directory.CreateDirectory(Path.Combine(project.AssetsDirectory, "Cabinet3D", "Test Cabinet")).FullName;
        var sourceGlb = Path.Combine(cabinetDir, "source.glb");
        WriteMinimalGlb(sourceGlb);
        var cabinetDocument = CabinetDocument.FromModelPath("source.glb")
            .WithTargetOverride(new CabinetTargetOverride("target-front", " INVERTED ", 90, true))
            .WithTargetOverride(new CabinetTargetOverride("target-back", CabinetTargetOverride.NormalFrontSide));
        var cabinetManifestPath = Path.Combine(cabinetDir, ProjectAssetPathService.Cabinet3DManifestFileName);
        File.WriteAllText(cabinetManifestPath, CabinetDocumentStorage.Serialize(cabinetDocument));
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
        var machineDocument = MachineDocumentExtensions.Empty("Test Machine") with
        {
            SurfaceAssignments =
            [
                SurfaceAssignment("target-front", "Front Face"),
                SurfaceAssignment("target-back", "Back Face")
            ]
        };
        var machineManifestPath = CreateMachineAsset(project, "Test Machine", ToCabinetAssetPath(cabinetManifestPath), machineDocument);

        var result = new MachineRuntimeBuildService().BuildFromMachineDocument(project, machineManifestPath, LoadMachine(machineManifestPath), NoOpEditorProgressReporter.Instance, CancellationToken.None);

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

        var updatedCabinet = cabinetDocument.WithTargetOverride(new CabinetTargetOverride("target-front", CabinetTargetOverride.NormalFrontSide, 270, false));
        File.WriteAllText(cabinetManifestPath, CabinetDocumentStorage.Serialize(updatedCabinet));
        var normalResult = new MachineRuntimeBuildService().BuildFromMachineDocument(project, machineManifestPath, LoadMachine(machineManifestPath), NoOpEditorProgressReporter.Instance, CancellationToken.None);
        Assert.True(normalResult.Success, normalResult.ErrorMessage);
        using var normalMachine = JsonDocument.Parse(File.ReadAllText(Path.Combine(normalResult.BuildRoot!, "machine.runtime.json")));
        var changedFace = Assert.Single(normalMachine.RootElement.GetProperty("faces").EnumerateArray(), candidate => candidate.GetProperty("faceId").GetString() == "face-runtime");
        Assert.Equal("normal", changedFace.GetProperty("frontSide").GetString());
        Assert.Equal(270, changedFace.GetProperty("faceRotation").GetInt32());
        Assert.False(changedFace.GetProperty("faceFlipHorizontal").GetBoolean());

        using var faceManifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(faceBuildDirectory, "face.runtime.json")));
        Assert.Equal(FaceRuntimeExportService.RuntimeManifestSchemaVersion, faceManifest.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("artwork.png", faceManifest.RootElement.GetProperty("artwork").GetString());
        Assert.Equal("mask.png", faceManifest.RootElement.GetProperty("mask").GetString());
    }

    [Fact]
    public void BuildFromMachineDocument_IgnoresInvalidUnreferencedFace()
    {
        var root = CreateTempRoot();
        var project = CreateProject(root);
        var cabinet = CreateCabinetWithSpec("source.glb", new CabinetTargetOverride("top", CabinetTargetOverride.NormalFrontSide));
        var cabinetManifestPath = CreateCabinetAsset(project, "Root Cabinet", cabinet);
        CreateFaceAssetWithReel(project, "Mounted", "mounted-face", "top", "standard");
        var brokenDirectory = Directory.CreateDirectory(Path.Combine(project.AssetsDirectory, "Faces", "Broken Unused")).FullName;
        File.WriteAllText(Path.Combine(brokenDirectory, ProjectAssetPathService.FaceManifestFileName), "{ not valid json");
        var machineDocument = MachineDocumentExtensions.Empty("Root Machine") with
        {
            SurfaceAssignments = [SurfaceAssignment("top", "Mounted")],
            ReelAssignments = [new MachineReelAssignment(MachineObjectReference.Reel(1), "standard")]
        };
        var machineManifestPath = CreateMachineAsset(project, "Root Machine", ToCabinetAssetPath(cabinetManifestPath), machineDocument);

        var result = new MachineRuntimeBuildService().BuildFromMachineDocument(project, machineManifestPath, LoadMachine(machineManifestPath), NoOpEditorProgressReporter.Instance, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(File.Exists(Path.Combine(result.BuildRoot!, "faces", "Mounted", FaceRuntimeExportService.ManifestFileName)));
    }

    [Fact]
    public void BuildFromMachineDocument_ReferencedInvalidFaceReportsCompositionPath()
    {
        var root = CreateTempRoot();
        var project = CreateProject(root);
        var cabinet = CabinetDocument.FromModelPath("source.glb");
        var cabinetManifestPath = CreateCabinetAsset(project, "Root Cabinet", cabinet);
        var brokenDirectory = Directory.CreateDirectory(Path.Combine(project.AssetsDirectory, "Faces", "Broken Mounted")).FullName;
        File.WriteAllText(Path.Combine(brokenDirectory, ProjectAssetPathService.FaceManifestFileName), "{ not valid json");
        var machineDocument = MachineDocumentExtensions.Empty("Root Machine") with
        {
            SurfaceAssignments = [SurfaceAssignment("top", "Broken Mounted")]
        };
        var machineManifestPath = CreateMachineAsset(project, "Root Machine", ToCabinetAssetPath(cabinetManifestPath), machineDocument);

        var result = new MachineRuntimeBuildService().BuildFromMachineDocument(project, machineManifestPath, LoadMachine(machineManifestPath), NoOpEditorProgressReporter.Instance, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Broken Mounted", result.ErrorMessage);
        Assert.Contains("invalid", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("top", result.ErrorMessage);
    }

    [Fact]
    public void BuildFromMachineDocument_ExportsReelDimensionsFromMachineCabinet()
    {
        var root = CreateTempRoot();
        var project = CreateProject(root);
        var cabinetManifestPath = CreateCabinetAsset(project, "Runtime Cabinet", CreateCabinetWithSpec("source.glb", new CabinetTargetOverride("bottomGlass", CabinetTargetOverride.NormalFrontSide)));
        CreateFaceAssetWithReel(project, "Bottom Face", "face-bottom", "bottomGlass", "standard");
        var machineDocument = MachineDocumentExtensions.Empty("Runtime Machine") with
        {
            SurfaceAssignments = [SurfaceAssignment("bottomGlass", "Bottom Face")],
            ReelAssignments = [new MachineReelAssignment(MachineObjectReference.Reel(1), "standard")]
        };
        var machineManifestPath = CreateMachineAsset(project, "Runtime Machine", ToCabinetAssetPath(cabinetManifestPath), machineDocument);

        var result = new MachineRuntimeBuildService().BuildFromMachineDocument(project, machineManifestPath, LoadMachine(machineManifestPath), NoOpEditorProgressReporter.Instance, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        using var faceManifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.BuildRoot!, "faces", "Bottom Face", "face.runtime.json")));
        var reel = Assert.Single(faceManifest.RootElement.GetProperty("reels").EnumerateArray());
        Assert.Equal(50, reel.GetProperty("physicalWidth").GetDouble());
        Assert.Equal(105, reel.GetProperty("physicalRadius").GetDouble());
    }

    [Fact]
    public void BuildFromMachineDocument_WithMissingCabinet_ReturnsClearFailure()
    {
        var root = CreateTempRoot();
        var project = CreateProject(root);
        var machineDir = Directory.CreateDirectory(Path.Combine(project.AssetsDirectory, "Machines", "Missing Cabinet Machine")).FullName;
        var machineManifestPath = Path.Combine(machineDir, ProjectAssetPathService.MachineManifestFileName);
        var machineDocument = MachineDocumentExtensions.Empty("Missing Cabinet Machine") with
        {
            CabinetAssetPath = "Assets/Cabinet3D/Missing Cabinet"
        };
        File.WriteAllText(machineManifestPath, MachineDocumentStorage.Serialize(machineDocument));

        var result = new MachineRuntimeBuildService().BuildFromMachineDocument(project, machineManifestPath, machineDocument, NoOpEditorProgressReporter.Instance, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Cabinet3D manifest was not found", result.ErrorMessage);
    }

    [Fact]
    public void BuildFromMachineDocument_WithNoCabinetAssigned_ReturnsClearFailure()
    {
        var root = CreateTempRoot();
        var project = CreateProject(root);
        var machineManifestPath = CreateMachineAsset(project, "No Cabinet", null, MachineDocumentExtensions.Empty("No Cabinet"));

        var result = new MachineRuntimeBuildService().BuildFromMachineDocument(project, machineManifestPath, LoadMachine(machineManifestPath), NoOpEditorProgressReporter.Instance, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("no assigned Cabinet", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildFromMachineDocument_AssignedTargetMismatch_ReturnsClearFailure()
    {
        var root = CreateTempRoot();
        var project = CreateProject(root);
        var cabinetDir = Directory.CreateDirectory(Path.Combine(project.AssetsDirectory, "Cabinet3D", "Mismatch Cabinet")).FullName;
        var sourceGlb = Path.Combine(cabinetDir, "source.glb");
        WriteMinimalGlb(sourceGlb);
        var cabinetManifestPath = Path.Combine(cabinetDir, ProjectAssetPathService.Cabinet3DManifestFileName);
        File.WriteAllText(cabinetManifestPath, CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("source.glb").WithTargetOverride(new CabinetTargetOverride("topGlass1", CabinetTargetOverride.InvertedFrontSide))));
        var faceDir = Directory.CreateDirectory(Path.Combine(project.AssetsDirectory, "Faces", "Mismatched Face")).FullName;
        WriteSolidPng(Path.Combine(faceDir, "artwork.png"), 4, 4, SKColors.Red);
        WriteSolidPng(Path.Combine(faceDir, "mask.png"), 4, 4, SKColors.White);
        File.WriteAllText(Path.Combine(faceDir, ProjectAssetPathService.FaceManifestFileName), FaceDocumentStorage.Serialize(CreateFaceDocument("face-mismatch", "OasisFace_Top-Glass 1", "Assets/Faces/Mismatched Face/artwork.png", "Assets/Faces/Mismatched Face/mask.png")));
        var machineDocument = MachineDocumentExtensions.Empty("Mismatch Machine") with
        {
            SurfaceAssignments = [SurfaceAssignment("OasisFace_Top-Glass 1", "Mismatched Face")]
        };
        var machineManifestPath = CreateMachineAsset(project, "Mismatch Machine", ToCabinetAssetPath(cabinetManifestPath), machineDocument);

        var result = new MachineRuntimeBuildService().BuildFromMachineDocument(project, machineManifestPath, LoadMachine(machineManifestPath), NoOpEditorProgressReporter.Instance, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("does not contain that target override", result.ErrorMessage);
        Assert.Contains("face-mismatch", result.ErrorMessage);
        Assert.Contains("OasisFace_Top-Glass 1", result.ErrorMessage);
        Assert.Contains("Assets/Cabinet3D/Mismatch Cabinet/asset.cabinet3d", result.ErrorMessage);
        Assert.Contains("topGlass1", result.ErrorMessage);
    }

    [Fact]
    public void BuildFromMachineDocument_MissingGlb_ReturnsClearFailure()
    {
        var root = CreateTempRoot();
        var project = CreateProject(root);
        var cabinetDir = Directory.CreateDirectory(Path.Combine(project.AssetsDirectory, "Cabinet3D", "Broken Cabinet")).FullName;
        var cabinetManifestPath = Path.Combine(cabinetDir, ProjectAssetPathService.Cabinet3DManifestFileName);
        File.WriteAllText(cabinetManifestPath, CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("missing.glb")));
        var machineManifestPath = CreateMachineAsset(project, "Broken Machine", ToCabinetAssetPath(cabinetManifestPath), MachineDocumentExtensions.Empty("Broken Machine"));

        var result = new MachineRuntimeBuildService().BuildFromMachineDocument(project, machineManifestPath, LoadMachine(machineManifestPath), NoOpEditorProgressReporter.Instance, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("GLB model was not found", result.ErrorMessage);
    }

    [Fact]
    public void BuildFromMachineDocument_ReportsMonotonicStageAndFaceProgress()
    {
        var root = CreateTempRoot();
        var project = CreateProject(root);
        var cabinet = CreateCabinetWithSpec("source.glb", new CabinetTargetOverride("top", CabinetTargetOverride.NormalFrontSide))
            .WithTargetOverride(new CabinetTargetOverride("bottom", CabinetTargetOverride.NormalFrontSide));
        var cabinetManifestPath = CreateCabinetAsset(project, "Progress Cabinet", cabinet);
        CreateFaceAssetWithReel(project, "Top Glass", "face-top", "top", "standard");
        CreateFaceAssetWithReel(project, "Bottom Glass", "face-bottom", "bottom", "standard");
        var machineDocument = MachineDocumentExtensions.Empty("Progress Machine") with
        {
            SurfaceAssignments = [SurfaceAssignment("top", "Top Glass"), SurfaceAssignment("bottom", "Bottom Glass")],
            ReelAssignments = [new MachineReelAssignment(MachineObjectReference.Reel(1), "standard")]
        };
        var machineManifestPath = CreateMachineAsset(project, "Progress Machine", ToCabinetAssetPath(cabinetManifestPath), machineDocument);
        var reports = new List<EditorProgressState>();
        var initial = new EditorProgressState("Build", "Starting", EditorProgressMode.Determinate, 0, false, false);
        var reporter = new EditorProgressReporter(initial, reports.Add);

        var result = new MachineRuntimeBuildService().BuildFromMachineDocument(project, machineManifestPath, LoadMachine(machineManifestPath), reporter, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        var determinate = reports.Where(report => report.Mode == EditorProgressMode.Determinate).ToArray();
        Assert.NotEmpty(determinate);
        Assert.True(determinate.Zip(determinate.Skip(1), (left, right) => left.Value.GetValueOrDefault() <= right.Value.GetValueOrDefault()).All(value => value));
        Assert.Equal(1d, determinate[^1].Value);
        Assert.Contains(reports, report => report.Message.Contains("Exporting Face 1 of 2", StringComparison.Ordinal));
        Assert.Contains(reports, report => report.Message.Contains("Exporting Face 2 of 2", StringComparison.Ordinal));
        Assert.Contains(reports, report => report.Message.Contains("Writing runtime manifests", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildFromMachineDocument_CancellationPreservesExistingBuildAndCleansStaging()
    {
        var root = CreateTempRoot();
        var project = CreateProject(root);
        var cabinet = CreateCabinetWithSpec("source.glb", new CabinetTargetOverride("top", CabinetTargetOverride.NormalFrontSide));
        var cabinetManifestPath = CreateCabinetAsset(project, "Cancellation Cabinet", cabinet);
        CreateFaceAssetWithReel(project, "Top Glass", "face-top", "top", "standard");
        var machineDocument = MachineDocumentExtensions.Empty("Cancellation Machine") with
        {
            SurfaceAssignments = [SurfaceAssignment("top", "Top Glass")],
            ReelAssignments = [new MachineReelAssignment(MachineObjectReference.Reel(1), "standard")]
        };
        var machineManifestPath = CreateMachineAsset(project, "Cancellation Machine", ToCabinetAssetPath(cabinetManifestPath), machineDocument);
        var service = new MachineRuntimeBuildService();
        var successful = service.BuildFromMachineDocument(project, machineManifestPath, LoadMachine(machineManifestPath), NoOpEditorProgressReporter.Instance, CancellationToken.None);
        Assert.True(successful.Success, successful.ErrorMessage);
        var markerPath = Path.Combine(successful.BuildRoot!, "existing-build.marker");
        File.WriteAllText(markerPath, "keep");
        using var cancellation = new CancellationTokenSource();
        var initial = new EditorProgressState("Build", "Starting", EditorProgressMode.Determinate, 0, true, false);
        var reporter = new EditorProgressReporter(initial, state =>
        {
            if (state.Message.Contains("Exporting Face", StringComparison.Ordinal)) cancellation.Cancel();
        });

        Assert.ThrowsAny<OperationCanceledException>(() => service.BuildFromMachineDocument(project, machineManifestPath, LoadMachine(machineManifestPath), reporter, cancellation.Token));

        Assert.True(File.Exists(markerPath));
        Assert.False(Directory.Exists(successful.BuildRoot + ".staging"));
    }

    private static MachineSurfaceAssignment SurfaceAssignment(string targetId, string assetName) =>
        new(targetId, $"Assets/Faces/{assetName}/{ProjectAssetPathService.FaceManifestFileName}");

    private static string CreateCabinetAsset(EditorProject project, string assetName, CabinetDocument cabinetDocument)
    {
        var cabinetDir = Directory.CreateDirectory(Path.Combine(project.AssetsDirectory, "Cabinet3D", assetName)).FullName;
        WriteMinimalGlb(Path.Combine(cabinetDir, "source.glb"));
        var manifestPath = Path.Combine(cabinetDir, ProjectAssetPathService.Cabinet3DManifestFileName);
        File.WriteAllText(manifestPath, CabinetDocumentStorage.Serialize(cabinetDocument));
        return manifestPath;
    }

    private static string CreateMachineAsset(EditorProject project, string assetName, string? cabinetAssetPath, MachineDocument machineDocument)
    {
        var machineDir = Directory.CreateDirectory(Path.Combine(project.AssetsDirectory, "Machines", assetName)).FullName;
        var manifestPath = Path.Combine(machineDir, ProjectAssetPathService.MachineManifestFileName);
        var document = machineDocument with { CabinetAssetPath = cabinetAssetPath };
        File.WriteAllText(manifestPath, MachineDocumentStorage.Serialize(document));
        return manifestPath;
    }

    private static MachineDocument LoadMachine(string manifestPath)
    {
        Assert.True(MachineDocumentStorage.TryRead(File.ReadAllText(manifestPath), out var document));
        return document;
    }

    private static string ToCabinetAssetPath(string cabinetManifestPath)
    {
        var directory = Path.GetDirectoryName(cabinetManifestPath)!;
        var assetsIndex = directory.IndexOf("Assets", StringComparison.OrdinalIgnoreCase);
        return ProjectAssetPathService.NormalizeProjectRelativePath(directory[assetsIndex..].Replace('\\', '/'));
    }

    private static CabinetDocument CreateCabinetWithSpec(string modelPath, CabinetTargetOverride targetOverride) => new(
        7,
        new CabinetModelReference(modelPath, 1.0, "Y"),
        [targetOverride],
        CabinetPreviewSettings.Default,
        [new CabinetReelSpecification("standard", "Standard", 210, 50)],
        "standard");

    private static void CreateFaceAssetWithReel(EditorProject project, string assetName, string faceId, string targetId, string reelSpecificationId)
    {
        var faceDir = Directory.CreateDirectory(Path.Combine(project.AssetsDirectory, "Faces", assetName)).FullName;
        var artworkPath = Path.Combine(faceDir, "artwork.png");
        var maskPath = Path.Combine(faceDir, "mask.png");
        WriteSolidPng(artworkPath, 4, 4, SKColors.Red);
        WriteSolidPng(maskPath, 4, 4, SKColors.White);
        var document = new FaceDocumentModel
        {
            Id = faceId,
            Title = assetName,
            SourceRegion = new FaceSourceRegionModel { X = 0, Y = 0, Width = 4, Height = 4 },
            MaskLayer = new FaceMaskLayerModel { AssetPath = ProjectAssetPathService.NormalizeProjectRelativePath(Path.GetRelativePath(project.ProjectDirectory, maskPath)), Width = 4, Height = 4 },
            Elements =
            [
                new FaceArtworkElement { ObjectId = "artwork", Name = "Artwork", X = 0, Y = 0, Width = 4, Height = 4, IsVisible = true, AssetPath = ProjectAssetPathService.NormalizeProjectRelativePath(Path.GetRelativePath(project.ProjectDirectory, artworkPath)) },
                new FaceReelDisplayElement { ObjectId = "reel-1", Name = "Reel 1", X = 1, Y = 1, Width = 100, Height = 200, Stops = 20, LinkedMachineObjectReference = MachineObjectReference.Reel(1) }
            ]
        };
        File.WriteAllText(Path.Combine(faceDir, ProjectAssetPathService.FaceManifestFileName), FaceDocumentStorage.Serialize(document));
    }

    private static FaceDocumentModel CreateFaceDocument(string faceId, string targetId, string artworkPath, string maskPath)
    {
        return new FaceDocumentModel
        {
            Id = faceId,
            Title = "Front Face",
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

    private static void WriteMinimalGlb(string path)
    {
        const string json = "{\"asset\":{\"version\":\"2.0\"},\"scene\":0,\"scenes\":[{}]} ";
        var jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write(0x46546C67u);
        writer.Write(2u);
        writer.Write((uint)(12 + 8 + jsonBytes.Length));
        writer.Write((uint)jsonBytes.Length);
        writer.Write(0x4E4F534Au);
        writer.Write(jsonBytes);
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

    private static EditorProject CreateProject(string root) => TestProjectFactory.Create(root);

    private static string CreateTempRoot() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "OasisEditorTests", Guid.NewGuid().ToString("N"))).FullName;
}
