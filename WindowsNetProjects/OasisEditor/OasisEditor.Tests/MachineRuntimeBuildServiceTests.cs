using OasisEditor.Progress;
using OasisEditor.Features.CabinetEditor.Models;
using System.Text;
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
    public void RuntimeFaceMapping_SparseOverridesUseDefaultsForUnconfiguredTarget()
    {
        var cabinet = CabinetDocument.FromModelPath("cabinet.glb") with
        {
            TargetOverrides = [new CabinetTargetOverride("bottomGlass", CabinetTargetOverride.InvertedFrontSide, 90, true)]
        };

        var top = MachineRuntimeBuildService.CreateRuntimeFaceReference(cabinet, "top-id", "TopGlass", "topGlass", "faces/top/face.runtime.json");
        var bottom = MachineRuntimeBuildService.CreateRuntimeFaceReference(cabinet, "bottom-id", "BottomGlass", "bottomGlass", "faces/bottom/face.runtime.json");

        Assert.Equal(CabinetTargetOverride.NormalFrontSide, top.FrontSide);
        Assert.Equal(0, top.FaceRotation);
        Assert.False(top.FaceFlipHorizontal);
        Assert.Equal(CabinetTargetOverride.InvertedFrontSide, bottom.FrontSide);
        Assert.Equal(90, bottom.FaceRotation);
        Assert.True(bottom.FaceFlipHorizontal);
    }

    [Fact]
    public void RuntimeFaceMapping_EmptyOverridesUseDefaultsForEveryTarget()
    {
        var cabinet = CabinetDocument.FromModelPath("cabinet.glb");
        foreach (var targetId in new[] { "topGlass", "bottomGlass" })
        {
            var reference = MachineRuntimeBuildService.CreateRuntimeFaceReference(cabinet, targetId + "-face", targetId, targetId, "face.runtime.json");
            Assert.Equal(CabinetTargetOverride.NormalFrontSide, reference.FrontSide);
            Assert.Equal(0, reference.FaceRotation);
            Assert.False(reference.FaceFlipHorizontal);
        }
    }

    [Fact]
    public void TargetValidationUsesDetectedGlbTargets_NotSparseOverrides()
    {
        Directory.CreateDirectory(_root);
        var glb = Path.Combine(_root, "cabinet.glb");
        WriteTwoTargetGlb(glb);

        MachineRuntimeBuildService.ValidateFaceAssignmentTargets(
            [new("topGlass", "Assets/Faces/Top/asset.face"), new("bottomGlass", "Assets/Faces/Bottom/asset.face")],
            glb, "Assets/Cabinet3D/Cabinet/asset.cabinet3d", CancellationToken.None);
        var exception = Assert.Throws<InvalidOperationException>(() => MachineRuntimeBuildService.ValidateFaceAssignmentTargets(
            [new("doesNotExist", "Assets/Faces/Missing/asset.face")], glb, "Assets/Cabinet3D/Cabinet/asset.cabinet3d", CancellationToken.None));
        Assert.Contains("not a valid detected OasisFace_* target", exception.Message);
        Assert.DoesNotContain("override", exception.Message, StringComparison.OrdinalIgnoreCase);
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

    private static void WriteTwoTargetGlb(string path)
    {
        var binary = new byte[92];
        var positions = new float[] { 0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0 };
        var uvs = new float[] { 0, 0, 1, 0, 1, 1, 0, 1 };
        var indices = new ushort[] { 0, 1, 2, 0, 2, 3 };
        Buffer.BlockCopy(positions, 0, binary, 0, positions.Length * sizeof(float));
        Buffer.BlockCopy(uvs, 0, binary, 48, uvs.Length * sizeof(float));
        Buffer.BlockCopy(indices, 0, binary, 80, indices.Length * sizeof(ushort));
        var json = """{"asset":{"version":"2.0"},"scene":0,"scenes":[{"nodes":[0,1]}],"nodes":[{"name":"OasisFace_topGlass","mesh":0},{"name":"OasisFace_bottomGlass","mesh":1}],"meshes":[{"primitives":[{"attributes":{"POSITION":0,"TEXCOORD_0":1},"indices":2}]},{"primitives":[{"attributes":{"POSITION":0,"TEXCOORD_0":1},"indices":2}]}],"buffers":[{"byteLength":92}],"bufferViews":[{"buffer":0,"byteOffset":0,"byteLength":48},{"buffer":0,"byteOffset":48,"byteLength":32},{"buffer":0,"byteOffset":80,"byteLength":12}],"accessors":[{"bufferView":0,"componentType":5126,"count":4,"type":"VEC3"},{"bufferView":1,"componentType":5126,"count":4,"type":"VEC2"},{"bufferView":2,"componentType":5123,"count":6,"type":"SCALAR"}]}""";
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
