using OasisEditor.Progress;
using OasisEditor.Features.CabinetEditor.Models;
using System.Text;
using System.Text.Json;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MachineRuntimeBuildServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OasisMachineBuild_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void BuildFromMachineDocument_MissingCabinetReportsMachineContext()
    {
        var project = Project();
        var machine = MachineDocument.Create("Bonanza") with { CabinetAssetPath = "Assets/Cabinet3D/Missing/asset.cabinet3d" };
        var path = WriteMachine(project, machine);
        var result = new MachineRuntimeBuildService().BuildFromMachineDocument(project, path, NoOpEditorProgressReporter.Instance, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Contains("Bonanza", result.ErrorMessage);
        Assert.Contains("missing Cabinet", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildFromMachineDocument_RequiresCanonicalMachinePackage()
    {
        var project = Project();
        var path = Path.Combine(_root, "loose.machine");
        var machine = MachineDocument.Create("Loose");
        File.WriteAllText(path, MachineDocumentStorage.Serialize(machine));
        var result = new MachineRuntimeBuildService().BuildFromMachineDocument(project, path, machine, NoOpEditorProgressReporter.Instance, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Contains("Assets/Machines", result.ErrorMessage);
    }

    [Fact]
    public void BuildRootUsesMachinePackageName_NotEditableDisplayName()
    {
        var project = Project();
        var service = new MachineRuntimeBuildService();
        var firstManifest = new ProjectAssetPathService().GetMachineManifestPath(project, "PartyTimeSlave1");
        var secondManifest = new ProjectAssetPathService().GetMachineManifestPath(project, "PartyTimeSlave2");
        var firstMachine = MachineDocument.Create("Party Time");
        var secondMachine = MachineDocument.Create("Party Time");
        var firstAssetName = ProjectAssetPathService.GetPackageAssetNameFromManifestPath(firstManifest, EditorAssetType.Machine);
        var secondAssetName = ProjectAssetPathService.GetPackageAssetNameFromManifestPath(secondManifest, EditorAssetType.Machine);

        var firstRoot = service.GetBuildRoot(project, firstAssetName!);
        var secondRoot = service.GetBuildRoot(project, secondAssetName!);

        Assert.Equal(firstMachine.DisplayName, secondMachine.DisplayName);
        Assert.NotEqual(firstRoot, secondRoot);
        Assert.EndsWith(Path.Combine("Builds", "PartyTimeSlave1"), firstRoot);
        Assert.EndsWith(Path.Combine("Builds", "PartyTimeSlave2"), secondRoot);
        Assert.DoesNotContain("Party Time", firstRoot);
        firstMachine = firstMachine with { DisplayName = "Renamed Party Time" };
        Assert.Equal("Renamed Party Time", firstMachine.DisplayName);
        Assert.Equal(firstRoot, service.GetBuildRoot(project, firstAssetName!));
    }

    [Fact]
    public void RuntimeFaceMapping_SparseSettingsUseDefaultsForUnconfiguredTarget()
    {
        var cabinet = CabinetDocument.FromModelPath("cabinet.glb") with
        {
            SurfaceTargetSettings = [new CabinetSurfaceTargetSettings("bottomGlass", CabinetSurfaceTargetSettings.InvertedFrontSide, 90, true)]
        };

        var top = MachineRuntimeBuildService.CreateRuntimeFaceReference(cabinet, "top-id", "TopGlass", "topGlass", "faces/top/face.runtime.json");
        var bottom = MachineRuntimeBuildService.CreateRuntimeFaceReference(cabinet, "bottom-id", "BottomGlass", "bottomGlass", "faces/bottom/face.runtime.json");

        Assert.Equal(CabinetSurfaceTargetSettings.NormalFrontSide, top.FrontSide);
        Assert.Equal(0, top.FaceRotation);
        Assert.False(top.FaceFlipHorizontal);
        Assert.Equal(CabinetSurfaceTargetSettings.InvertedFrontSide, bottom.FrontSide);
        Assert.Equal(90, bottom.FaceRotation);
        Assert.True(bottom.FaceFlipHorizontal);
    }

    [Fact]
    public void RuntimeFaceMapping_EmptySettingsUseDefaultsForEveryTarget()
    {
        var cabinet = CabinetDocument.FromModelPath("cabinet.glb");
        foreach (var targetId in new[] { "topGlass", "bottomGlass" })
        {
            var reference = MachineRuntimeBuildService.CreateRuntimeFaceReference(cabinet, targetId + "-face", targetId, targetId, "face.runtime.json");
            Assert.Equal(CabinetSurfaceTargetSettings.NormalFrontSide, reference.FrontSide);
            Assert.Equal(0, reference.FaceRotation);
            Assert.False(reference.FaceFlipHorizontal);
        }
    }

    [Fact]
    public void TargetValidationUsesDetectedGlbTargets_NotSparseSettings()
    {
        Directory.CreateDirectory(_root);
        var glb = Path.Combine(_root, "cabinet.glb");
        WriteTwoTargetGlb(glb);

        MachineRuntimeBuildService.ValidateFaceAssignmentTargets("Machine",
            [new("topGlass", "Assets/Faces/Top/asset.face"), new("bottomGlass", "Assets/Faces/Bottom/asset.face")],
            glb, "Assets/Cabinet3D/Cabinet/asset.cabinet3d", CancellationToken.None);
        var exception = Assert.Throws<InvalidOperationException>(() => MachineRuntimeBuildService.ValidateFaceAssignmentTargets(
            "Machine", [new("doesNotExist", "Assets/Faces/Missing/asset.face")], glb, "Assets/Cabinet3D/Cabinet/asset.cabinet3d", CancellationToken.None));
        Assert.Contains("not a valid detected OasisFace_* target", exception.Message);
        Assert.DoesNotContain("override", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TargetValidationAllowsNoDetectedTargetsOnlyWhenMachineHasNoAssignments()
    {
        Directory.CreateDirectory(_root);
        var glb = Path.Combine(_root, "cabinet.glb");
        WriteTwoTargetGlb(glb, "Cabinet_topGlass", "Cabinet_bottomGlass");

        MachineRuntimeBuildService.ValidateFaceAssignmentTargets(
            "Empty Machine", [], glb, "Assets/Cabinet3D/Cabinet/asset.cabinet3d", CancellationToken.None);

        var exception = Assert.Throws<InvalidOperationException>(() => MachineRuntimeBuildService.ValidateFaceAssignmentTargets(
            "Assigned Machine", [new("topGlass", "Assets/Faces/Top/asset.face")], glb,
            "Assets/Cabinet3D/Cabinet/asset.cabinet3d", CancellationToken.None));
        Assert.Contains("Assigned Machine", exception.Message);
        Assert.Contains("topGlass", exception.Message);
        Assert.Contains("no valid OasisFace_* targets", exception.Message);
    }

    [Fact]
    public void TargetValidationRejectsAssignmentWhenOnlyAnotherTargetIsDetected()
    {
        Directory.CreateDirectory(_root);
        var glb = Path.Combine(_root, "cabinet.glb");
        WriteTwoTargetGlb(glb, "Cabinet_topGlass", "OasisFace_bottomGlass");

        var exception = Assert.Throws<InvalidOperationException>(() => MachineRuntimeBuildService.ValidateFaceAssignmentTargets(
            "Machine", [new("topGlass", "Assets/Faces/Top/asset.face")], glb,
            "Assets/Cabinet3D/Cabinet/asset.cabinet3d", CancellationToken.None));
        Assert.Contains("topGlass", exception.Message);
        Assert.Contains("bottomGlass", exception.Message);
    }

    [Fact]
    public void Build_ResolvesReferencedReelAssetIntoFaceRuntimeDimensions()
    {
        var setup = CreateReelBuild([0], [(0, "Standard", 290d, 70d)]);
        var result = Build(setup.Project, setup.Machine);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(new[] { (70d, 145d) }, ReadRuntimeReelDimensions(result.BuildRoot!));
    }

    [Fact]
    public void Build_MultipleLogicalReelsCanShareOneAsset()
    {
        var setup = CreateReelBuild([0, 1, 2], [(0, "Standard", 290d, 70d), (1, "Standard", 290d, 70d), (2, "Standard", 290d, 70d)]);
        var result = Build(setup.Project, setup.Machine);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(new[] { (70d, 145d), (70d, 145d), (70d, 145d) }, ReadRuntimeReelDimensions(result.BuildRoot!));
    }

    [Fact]
    public void Build_DifferentLogicalReelsResolveDifferentAssets()
    {
        var setup = CreateReelBuild([0, 3], [(0, "Standard", 290d, 70d), (3, "Small", 230d, 60d)]);
        var result = Build(setup.Project, setup.Machine);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(new[] { (70d, 145d), (60d, 115d) }, ReadRuntimeReelDimensions(result.BuildRoot!));
    }

    [Fact]
    public void Build_MissingLogicalAssignmentNamesMachineAndReel()
    {
        var setup = CreateReelBuild([3], []);
        var result = Build(setup.Project, setup.Machine);
        Assert.False(result.Success);
        Assert.Contains("Reel Machine", result.ErrorMessage);
        Assert.Contains("reel:3", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_MissingReelAssetNamesMachineLogicalReelAndPath()
    {
        var setup = CreateReelBuild([3], []);
        const string missing = "Assets/Reels/Small/asset.reel";
        var machine = setup.Machine with { ReelAssignments = [new(MachineObjectReference.Reel(3), missing)] };
        var result = Build(setup.Project, machine);
        Assert.False(result.Success);
        Assert.Contains("Reel Machine", result.ErrorMessage);
        Assert.Contains("reel:3", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(missing, result.ErrorMessage);
    }

    [Fact]
    public void Build_InvalidReferencedReelFailsButUnusedBrokenReelIsIgnored()
    {
        var setup = CreateReelBuild([0], [(0, "Standard", 290d, 70d)]);
        var unused = new ProjectAssetPathService().GetReelManifestPath(setup.Project, "Broken");
        Directory.CreateDirectory(Path.GetDirectoryName(unused)!);
        File.WriteAllText(unused, "{ broken");
        Assert.True(Build(setup.Project, setup.Machine).Success);

        var standard = new ProjectAssetPathService().GetReelManifestPath(setup.Project, "Standard");
        File.WriteAllText(standard, "{ \"version\": 99 }");
        var invalid = Build(setup.Project, setup.Machine);
        Assert.False(invalid.Success);
        Assert.Contains("invalid", invalid.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Assets/Reels/Standard/asset.reel", invalid.ErrorMessage);
    }

    private (EditorProject Project, MachineDocument Machine) CreateReelBuild(int[] logicalReels, (int Logical, string Asset, double Diameter, double Width)[] assignments)
    {
        var project = Project();
        var paths = new ProjectAssetPathService();
        var cabinetManifest = paths.GetCabinet3DManifestPath(project, "Cabinet");
        Directory.CreateDirectory(Path.GetDirectoryName(cabinetManifest)!);
        WriteTwoTargetGlb(Path.Combine(Path.GetDirectoryName(cabinetManifest)!, "cabinet.glb"), "OasisFace_glass", "unused");
        File.WriteAllText(cabinetManifest, CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("cabinet.glb")));
        var faceManifest = paths.GetFaceManifestPath(project, "Glass");
        Directory.CreateDirectory(Path.GetDirectoryName(faceManifest)!);
        var maskPath = Path.Combine(Path.GetDirectoryName(faceManifest)!, "mask.png");
        using (var bitmap = new SKBitmap(2, 2))
        {
            bitmap.Erase(SKColors.White);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(maskPath);
            data.SaveTo(stream);
        }
        var face = new FaceDocumentModel
        {
            Id = Guid.NewGuid().ToString("D"), Title = "Glass", SourceRegion = new FaceSourceRegionModel { Width = 100, Height = 100 },
            MaskLayer = new FaceMaskLayerModel { AssetPath = paths.ToProjectRelativePath(project, maskPath), Width = 2, Height = 2 },
            Elements = logicalReels.Select((logical, index) => (FaceElementModel)new FaceReelMount { ObjectId = $"reel-{logical}", Name = $"Reel {logical}", X = index * 10, Y = 0, Width = 10, Height = 20, Stops = 20, LinkedMachineObjectReference = MachineObjectReference.Reel(logical) }).ToArray()
        };
        File.WriteAllText(faceManifest, FaceDocumentStorage.Serialize(face));
        foreach (var asset in assignments.GroupBy(value => value.Asset).Select(group => group.First()))
        {
            var reelManifest = paths.GetReelManifestPath(project, asset.Asset);
            Directory.CreateDirectory(Path.GetDirectoryName(reelManifest)!);
            File.WriteAllText(reelManifest, ReelDocumentStorage.Serialize(ReelDocument.Create(asset.Asset) with { DiameterMm = asset.Diameter, WidthMm = asset.Width }));
        }
        var machine = MachineDocument.Create("Reel Machine") with
        {
            CabinetAssetPath = paths.ToProjectRelativePath(project, cabinetManifest),
            SurfaceAssignments = [new("glass", paths.ToProjectRelativePath(project, faceManifest))],
            ReelAssignments = assignments.Select(value => new MachineReelAssignment(MachineObjectReference.Reel(value.Logical), paths.ToProjectRelativePath(project, paths.GetReelManifestPath(project, value.Asset)))).ToArray()
        };
        return (project, machine);
    }

    private MachineRuntimeBuildResult Build(EditorProject project, MachineDocument machine)
        => new MachineRuntimeBuildService().BuildFromMachineDocument(project, WriteMachine(project, machine), NoOpEditorProgressReporter.Instance, CancellationToken.None);

    private static (double Width, double Radius)[] ReadRuntimeReelDimensions(string buildRoot)
    {
        var path = Directory.EnumerateFiles(Path.Combine(buildRoot, "faces"), FaceRuntimeExportService.ManifestFileName, SearchOption.AllDirectories).Single();
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        return json.RootElement.GetProperty("reels").EnumerateArray().Select(reel => (reel.GetProperty("physicalWidth").GetDouble(), reel.GetProperty("physicalRadius").GetDouble())).ToArray();
    }

    private EditorProject Project()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Assets", "Machines"));
        Directory.CreateDirectory(Path.Combine(_root, "Generated"));
        return new EditorProject { Name = "Workspace", ProjectFilePath = Path.Combine(_root, "Workspace.oasisproj"), ProjectDirectory = _root, AssetsDirectory = Path.Combine(_root, "Assets"), GeneratedDirectory = Path.Combine(_root, "Generated") };
    }

    private static string WriteMachine(EditorProject project, MachineDocument machine)
    {
        var path = new ProjectAssetPathService().GetMachineManifestPath(project, machine.DisplayName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, MachineDocumentStorage.Serialize(machine));
        return path;
    }

    private static void WriteTwoTargetGlb(string path, string firstNodeName = "OasisFace_topGlass", string secondNodeName = "OasisFace_bottomGlass")
    {
        var binary = new byte[92];
        var positions = new float[] { 0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0 };
        var uvs = new float[] { 0, 0, 1, 0, 1, 1, 0, 1 };
        var indices = new ushort[] { 0, 1, 2, 0, 2, 3 };
        Buffer.BlockCopy(positions, 0, binary, 0, positions.Length * sizeof(float));
        Buffer.BlockCopy(uvs, 0, binary, 48, uvs.Length * sizeof(float));
        Buffer.BlockCopy(indices, 0, binary, 80, indices.Length * sizeof(ushort));
        var json = $$"""{"asset":{"version":"2.0"},"scene":0,"scenes":[{"nodes":[0,1]}],"nodes":[{"name":"{{firstNodeName}}","mesh":0},{"name":"{{secondNodeName}}","mesh":1}],"meshes":[{"primitives":[{"attributes":{"POSITION":0,"TEXCOORD_0":1},"indices":2}]},{"primitives":[{"attributes":{"POSITION":0,"TEXCOORD_0":1},"indices":2}]}],"buffers":[{"byteLength":92}],"bufferViews":[{"buffer":0,"byteOffset":0,"byteLength":48},{"buffer":0,"byteOffset":48,"byteLength":32},{"buffer":0,"byteOffset":80,"byteLength":12}],"accessors":[{"bufferView":0,"componentType":5126,"count":4,"type":"VEC3"},{"bufferView":1,"componentType":5126,"count":4,"type":"VEC2"},{"bufferView":2,"componentType":5123,"count":6,"type":"SCALAR"}]}""";
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var paddedJsonLength = (jsonBytes.Length + 3) & ~3;
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write(0x46546C67); writer.Write(2); writer.Write(12 + 8 + paddedJsonLength + 8 + binary.Length);
        writer.Write(paddedJsonLength); writer.Write(0x4E4F534A); writer.Write(jsonBytes); writer.Write(Enumerable.Repeat((byte)0x20, paddedJsonLength - jsonBytes.Length).ToArray());
        writer.Write(binary.Length); writer.Write(0x004E4942); writer.Write(binary);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
