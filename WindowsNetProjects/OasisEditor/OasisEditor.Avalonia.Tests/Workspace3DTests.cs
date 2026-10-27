using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using OasisEditor;
using OasisEditor.Avalonia.Rendering3D;
using OasisEditor.Features.CabinetEditor.Models;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class Workspace3DTests
{
    [Fact]
    public async Task NestedPrefabModelPicksOwningNodeAndLiveEditsUndoAndPersist()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var workspace = NewWorkspace();
            Directory.CreateDirectory(workspace);
            try
            {
                var project = new AuthoringSession(workspace);
                project.CreateProject("3D workspace test");
                var projectRoot = Path.GetDirectoryName(project.ProjectFilePath!)!;
                File.WriteAllBytes(Path.Combine(projectRoot, "Assets", "Quad.glb"), CreateQuadGlb());
                using var capture = new WorkspaceCapture(project);
                var view = capture.View;
                var manifest = view.Prefabs.Create("Nested");
                var rootId = view.Prefabs.Document.Root.Id;
                var childId = view.Prefabs.AddNode(rootId, "Model child");
                view.Prefabs.ImportModel(childId, "Assets/Quad.glb");
                view.Prefabs.Save();

                await view.OpenPrefab3DAsync(Path.GetRelativePath(projectRoot, manifest));
                await view.WaitForSceneAsync();
                view.SceneView.SetCamera(new SceneCameraState(Vector3.Zero, 4, 0, 0));
                var pick = view.PickScene3D(0, 0);
                Assert.NotNull(pick);
                Assert.Equal(childId, pick!.GetType().GetProperty("nodeId")!.GetValue(pick));

                var moved = new MachineObjectTransform(new(2, 0, 0), MachineVector3.Zero, MachineVector3.One);
                view.Prefabs.SetTransform(childId, moved);
                view.Prefabs.SetCollider(childId, new(Object3DColliderKind.Box, Center: [0, 0, 0], Size: [1, 2, 3]));
                await view.WaitForSceneAsync();
                var childMesh = Assert.Single(view.SceneView.Scene.Meshes, mesh => mesh.Name == "Model" && mesh.Kind == "visual");
                Assert.Equal(2, childMesh.Transform.M41, 4);
                Assert.Contains(view.SceneView.Scene.Meshes, mesh => mesh.Kind == "collider");
                Assert.True(view.Prefabs.IsDirty);

                view.Prefabs.Undo();
                await view.WaitForSceneAsync();
                Assert.DoesNotContain(view.SceneView.Scene.Meshes, mesh => mesh.Kind == "collider");
                Assert.Equal(2, Assert.Single(view.SceneView.Scene.Meshes, mesh => mesh.Name == "Model" && mesh.Kind == "visual").Transform.M41, 4);
                view.Prefabs.Redo();
                await view.WaitForSceneAsync();
                Assert.Contains(view.SceneView.Scene.Meshes, mesh => mesh.Kind == "collider");

                view.Prefabs.Save();
                var reopened = new PrefabAuthoringSession(project);
                reopened.Open(manifest);
                var savedChild = reopened.Document.Root.Children.Single(node => node.Id == childId);
                Assert.Equal(moved, savedChild.Transform);
                Assert.Equal(Object3DColliderKind.Box, savedChild.Components.Single(component => component.Kind == Object3DComponentDefinition.ColliderKind).Collider!.Kind);
                Assert.False(reopened.IsDirty);
            }
            finally { if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true); }
        });
    }

    [Fact]
    public async Task MissingAssignedNativeFaceIsIncludedInSceneDiagnostics()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var workspace = NewWorkspace();
            Directory.CreateDirectory(workspace);
            try
            {
                var project = new AuthoringSession(workspace);
                project.CreateProject("Face scene test");
                var projectRoot = Path.GetDirectoryName(project.ProjectFilePath!)!;
                File.WriteAllBytes(Path.Combine(projectRoot, "Assets", "FaceCabinet.glb"), CreateQuadGlb(faceTarget: true));
                using var capture = new WorkspaceCapture(project);
                var view = capture.View;
                var cabinetPath = view.Cabinets.Create("Cabinet", "Assets/FaceCabinet.glb");
                var cabinetRelative = Path.GetRelativePath(projectRoot, cabinetPath).Replace('\\', '/');
                view.Machines.CreateMachine("Machine");
                view.Machines.UpdateDocument(machine => machine with
                {
                    CabinetAsset = AssetReference.Project(cabinetRelative),
                    SurfaceAssignments = [new MachineSurfaceAssignment("screen", "Assets/Faces/Missing/face.json")]
                });
                view.ShowMachine();
                await view.WaitForSceneAsync();

                Assert.Contains(view.SceneView.Scene.Diagnostics, message => message.Contains("Face target 'screen':", StringComparison.OrdinalIgnoreCase));
            }
            finally { if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true); }
        });
    }

    private static string NewWorkspace() => Path.Combine(Path.GetTempPath(), "OasisWorkspace3D-" + Guid.NewGuid().ToString("N"));

    private static byte[] CreateQuadGlb(bool faceTarget = false)
    {
        var binary = new byte[140];
        WriteFloats(binary, 0, [-1, -1, 0, 1, -1, 0, 1, 1, 0, -1, 1, 0]);
        WriteFloats(binary, 48, [0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1]);
        WriteFloats(binary, 96, [0, 0, 1, 0, 1, 1, 0, 1]);
        for (var i = 0; i < 6; i++) BinaryPrimitives.WriteUInt16LittleEndian(binary.AsSpan(128 + i * 2, 2), new ushort[] { 0, 1, 2, 0, 2, 3 }[i]);
        var sceneNodes = faceTarget ? "[0,1]" : "[0]";
        var nodes = faceTarget
            ? "{\"name\":\"OasisFace_screen\",\"mesh\":0},{\"name\":\"CabinetShell\",\"mesh\":0}"
            : "{\"name\":\"Model\",\"mesh\":0}";
        var json = $$"""
        {"asset":{"version":"2.0"},"scene":0,"scenes":[{"nodes":{{sceneNodes}}}],
         "nodes":[{{nodes}}],
         "meshes":[{"name":"Quad","primitives":[{"attributes":{"POSITION":0,"NORMAL":1,"TEXCOORD_0":2},"indices":3}]}],
         "buffers":[{"byteLength":140}],
         "bufferViews":[{"buffer":0,"byteOffset":0,"byteLength":48},{"buffer":0,"byteOffset":48,"byteLength":48},{"buffer":0,"byteOffset":96,"byteLength":32},{"buffer":0,"byteOffset":128,"byteLength":12}],
         "accessors":[{"bufferView":0,"componentType":5126,"count":4,"type":"VEC3","min":[-1,-1,0],"max":[1,1,0]},{"bufferView":1,"componentType":5126,"count":4,"type":"VEC3"},{"bufferView":2,"componentType":5126,"count":4,"type":"VEC2"},{"bufferView":3,"componentType":5123,"count":6,"type":"SCALAR"}]}
        """;
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var jsonLength = (jsonBytes.Length + 3) & ~3;
        var binaryLength = (binary.Length + 3) & ~3;
        var result = new byte[12 + 8 + jsonLength + 8 + binaryLength];
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0, 4), 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4, 4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8, 4), (uint)result.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12, 4), (uint)jsonLength);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(16, 4), 0x4E4F534A);
        jsonBytes.CopyTo(result.AsSpan(20));
        result.AsSpan(20 + jsonBytes.Length, jsonLength - jsonBytes.Length).Fill(0x20);
        var binaryHeader = 20 + jsonLength;
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(binaryHeader, 4), (uint)binaryLength);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(binaryHeader + 4, 4), 0x004E4942);
        binary.CopyTo(result.AsSpan(binaryHeader + 8));
        return result;
    }

    private static void WriteFloats(byte[] destination, int offset, float[] values)
    {
        for (var i = 0; i < values.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination.AsSpan(offset + i * sizeof(float), sizeof(float)), BitConverter.SingleToInt32Bits(values[i]));
    }
}
