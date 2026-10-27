using System.Numerics;
using Oasis.Rendering3D;
using OasisEditor;
using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.CabinetEditor.Services;
using Xunit;

namespace Oasis.Rendering3D.Tests;

public sealed class Portable3DCompositionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OasisPortable3DComposition", Guid.NewGuid().ToString("N"));

    [Fact]
    public void TransformMathMatchesAuthoredScaleThenZxyEulerThenTranslation()
    {
        var placement = new MachineObjectTransform(new(4, 5, 6), new(90, 0, 90), new(2, 3, 4));
        var actual = Vector3.Transform(new Vector3(1, 2, 3), Portable3DTransform.CreatePlacement(placement));
        var manuallyPlaced = Vector3.Transform(new Vector3(1, 2, 3), Matrix4x4.CreateScale(2, 3, 4));
        manuallyPlaced = Vector3.Transform(manuallyPlaced, Matrix4x4.CreateRotationZ(MathF.PI / 2));
        manuallyPlaced = Vector3.Transform(manuallyPlaced, Matrix4x4.CreateRotationX(MathF.PI / 2));
        manuallyPlaced = Vector3.Transform(manuallyPlaced, Matrix4x4.CreateRotationY(0));
        manuallyPlaced += new Vector3(4, 5, 6);
        AssertVector(manuallyPlaced, actual);
    }

    [Fact]
    public void IntrinsicTransformHandlesZAndXUpAxesBeforeAuthoredPlacement()
    {
        var z = Portable3DTransform.CreateIntrinsic(new Object3DModelDefinition("model.glb", 2, "Z"));
        var x = Portable3DTransform.CreateIntrinsic(new Object3DModelDefinition("model.glb", 2, "X"));
        AssertVector(new Vector3(0, 2, 0), Vector3.Transform(new Vector3(0, 0, 1), z));
        AssertVector(new Vector3(0, 2, 0), Vector3.Transform(new Vector3(1, 0, 0), x));
    }

    [Fact]
    public void FacePlanePreservesCornerRotationFlipUvAndFrontWinding()
    {
        var target = new CabinetFaceTarget("screen", "OasisFace_screen", "screen",
            [new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0)], Vector3.UnitZ, new(.5f, .5f, 0), true, null);
        var expectedFirst = new Dictionary<int, Vector3>
        {
            [0] = new(0, 0, 0), [90] = new(0, 1, 0), [180] = new(1, 1, 0), [270] = new(1, 0, 0)
        };
        foreach (var rotation in expectedFirst.Keys)
        {
            Assert.True(PortableFacePlaneGeometryBuilder.TryCreate(target, new("screen", "normal", rotation), "Faces/screen.png", Matrix4x4.Identity, out var plane));
            Assert.Equal(expectedFirst[rotation], plane.Positions[0]);
            Assert.Equal(new Vector2(0, 0), plane.TextureCoordinates[0]);
            Assert.Equal(new[] { 0, 1, 2, 0, 2, 3 }, plane.TriangleIndices);
        }

        Assert.True(PortableFacePlaneGeometryBuilder.TryCreate(target, new("screen", "inverted", 0, true), "Faces/screen.png", Matrix4x4.Identity, out var flipped));
        Assert.Equal(new Vector3(1, 0, 0), flipped.Positions[0]);
        AssertVector(-Vector3.UnitZ, flipped.Normal);
        Assert.Equal(new[] { 0, 1, 2, 0, 2, 3 }, flipped.TriangleIndices); // Flip and inverted side cancel winding, as in WPF.
    }

    [Fact]
    public async Task ComposesRealGlbThroughProjectAndLibraryPrefabsAndParentTransforms()
    {
        Directory.CreateDirectory(_root);
        var projectRoot = Path.Combine(_root, "Project");
        var libraryRoot = Path.Combine(_root, "Library");
        var projectPrefab = Path.Combine(projectRoot, "Assets", "Objects", "CabinetPart");
        var libraryPrefab = Path.Combine(libraryRoot, "Objects", "CabinetPart");
        Directory.CreateDirectory(projectPrefab);
        Directory.CreateDirectory(libraryPrefab);
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jGQAAAABJRU5ErkJggg==");
        PortableGlbSceneLoaderTests.WriteFixture(Path.Combine(projectPrefab, "model.glb"), png);
        PortableGlbSceneLoaderTests.WriteFixture(Path.Combine(libraryPrefab, "model.glb"), png);
        var definition = CreatePrefabWithColliders();
        var manifest = Path.Combine(projectPrefab, "prefab.json");
        var libraryManifest = Path.Combine(libraryPrefab, "prefab.json");
        File.WriteAllText(manifest, Object3DDocumentStorage.Serialize(definition));
        File.WriteAllText(libraryManifest, Object3DDocumentStorage.Serialize(definition));
        var project = new EditorProject
        {
            Name = "Test",
            ProjectFilePath = Path.Combine(projectRoot, "Test.oasisproj"),
            ProjectDirectory = projectRoot,
            AssetsDirectory = Path.Combine(projectRoot, "Assets"),
            GeneratedDirectory = Path.Combine(projectRoot, "Generated")
        };
        var prefabPath = "Assets/Objects/CabinetPart/prefab.json";
        var parent = new MachineObject3DInstance("parent", "Parent", AssetReference.Project(prefabPath),
            new(new(10, 0, 0), MachineVector3.Zero, MachineVector3.One));
        var child = new MachineObject3DInstance("child", "Child", AssetReference.Library("Objects/CabinetPart/prefab.json"),
            new(new(0, 5, 0), MachineVector3.Zero, MachineVector3.One), ParentId: "parent");
        var machine = MachineDocument.Create("Fixture") with
        {
            ObjectInstances = [parent, child],
            Anchors = [new MachineAnchor("anchor", "Socket", new(4, 5, 6), new(0, 0, 90))]
        };

        var result = await new Portable3DCompositionService().ComposeMachineAsync(project, machine, libraryRoot);

        Assert.DoesNotContain(result.Diagnostics, message => message.Contains("cannot be composed", StringComparison.OrdinalIgnoreCase));
        var parentVisual = Assert.Single(result.Meshes, mesh => mesh.OwnerId == "parent" && mesh.NodeId == definition.Root.Id + "/1" && mesh.SemanticKind == CabinetSemanticGeometryKind.Visual);
        var childVisual = Assert.Single(result.Meshes, mesh => mesh.OwnerId == "child" && mesh.NodeId == definition.Root.Id + "/1" && mesh.SemanticKind == CabinetSemanticGeometryKind.Visual);
        AssertVector(new Vector3(13, 3, 0), Vector3.Transform(Vector3.Zero, parentVisual.WorldTransform));
        AssertVector(new Vector3(14, 8, 0), Vector3.Transform(Vector3.Zero, childVisual.WorldTransform));
        Assert.Equal(4, result.Materials.Count); // Each distinct model path gets a default and a GLB material.
        Assert.Equal(6, result.Meshes.Count(mesh => mesh.SemanticKind == CabinetSemanticGeometryKind.Trigger));
        Assert.Equal(2, result.Meshes.Count(mesh => mesh.SemanticId == "PrefabCollider"));
        Assert.Equal(2, result.Meshes.Count(mesh => mesh.SemanticId == "PrefabTrigger"));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Contains("capsule collider preview", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Contains("mesh collider preview", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("anchor", Assert.Single(result.Anchors).Id);
        Assert.False(result.Bounds.IsEmpty);

        var unsavedDefinition = definition with { Root = definition.Root with { Transform = new(new(9, 0, 0), MachineVector3.Zero, MachineVector3.One) } };
        var unsavedPrefab = await new Portable3DCompositionService().ComposePrefabAsync(project, unsavedDefinition, manifest);
        var unsavedVisual = Assert.Single(unsavedPrefab.Meshes, mesh => mesh.NodeId.EndsWith(definition.Root.Id + "/1", StringComparison.Ordinal) && mesh.SemanticKind == CabinetSemanticGeometryKind.Visual);
        AssertVector(new Vector3(11, 3, 0), Vector3.Transform(Vector3.Zero, unsavedVisual.WorldTransform));
        var inactive = machine with { ObjectInstances = [parent with { Active = false }, child] };
        var inactiveResult = await new Portable3DCompositionService().ComposeMachineAsync(project, inactive, libraryRoot);
        Assert.Empty(inactiveResult.Meshes);
    }

    [Fact]
    public async Task ComposesCabinetFaceBindingWithAuthoredSettingsAndIntrinsicTransform()
    {
        Directory.CreateDirectory(_root);
        var projectRoot = Path.Combine(_root, "FaceProject");
        var package = Path.Combine(projectRoot, "Assets", "Cabinets", "Cabinet");
        Directory.CreateDirectory(package);
        var modelPath = Path.Combine(package, "cabinet.glb");
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jGQAAAABJRU5ErkJggg==");
        PortableGlbSceneLoaderTests.WriteFixture(modelPath, png, faceTarget: true);
        var settings = new CabinetSurfaceTargetSettings("screen", "inverted", 90, true);
        var savedCabinet = CabinetDocument.FromModelPath("cabinet.glb");
        var cabinet = savedCabinet with { Model = new("cabinet.glb", 2, "Z") };
        cabinet = cabinet.WithSurfaceTargetSettings(settings);
        var manifestPath = Path.Combine(package, "cabinet.json");
        File.WriteAllText(manifestPath, CabinetDocumentStorage.Serialize(savedCabinet));
        var project = new EditorProject
        {
            Name = "Face fixture", ProjectFilePath = Path.Combine(projectRoot, "Face.oasisproj"), ProjectDirectory = projectRoot,
            AssetsDirectory = Path.Combine(projectRoot, "Assets"), GeneratedDirectory = Path.Combine(projectRoot, "Generated")
        };
        var machine = MachineDocument.Create("Face fixture") with
        {
            SurfaceAssignments = [new MachineSurfaceAssignment("screen", "Faces/screen.face")]
        };

        var result = await new Portable3DCompositionService().ComposeCabinetAsync(project, cabinet, manifestPath, machine);
        var plane = Assert.Single(result.FacePlanes);
        var loadedTarget = Assert.Single((await new PortableGlbSceneLoader().LoadAsync(modelPath)).FaceTargets);
        var intrinsic = Portable3DTransform.CreateIntrinsic(cabinet.Model);
        var expectedWorldCorners = loadedTarget.Corners.Select(point => Vector3.Transform(point, intrinsic)).ToArray();

        Assert.Equal(cabinet, result.CabinetDocument);
        Assert.Single(result.CabinetFaceTargets);
        Assert.Equal(2, result.ReflectionTargets.Count);
        Assert.Equal(intrinsic, result.CabinetIntrinsicTransform);
        var unassigned = await new Portable3DCompositionService().ComposeCabinetAsync(project, cabinet, manifestPath);
        Assert.Empty(unassigned.FacePlanes);
        Assert.Single(unassigned.CabinetFaceTargets);
        Assert.Equal("cabinet", plane.OwnerId);
        Assert.Equal("screen", plane.TargetId);
        Assert.Equal("screen", plane.SourceTarget.Id);
        Assert.Equal(90, plane.Settings.FaceRotation);
        Assert.Equal("inverted", plane.Settings.FrontSide);
        Assert.Equal(intrinsic, plane.IntrinsicTransform);
        Assert.Equal("Faces/screen.face", plane.FaceAssetPath);
        Assert.Equal(4, plane.Positions.Length);
        Assert.All(plane.Positions, point => Assert.Contains(expectedWorldCorners, corner => Vector3.DistanceSquared(corner, point) < 1e-6f));
        Assert.Equal(new[] { 0, 1, 2, 0, 2, 3 }, plane.TriangleIndices); // Horizontal flip plus inverted front preserves the existing winding.
        Assert.Empty(result.Diagnostics);
    }
    private static Object3DDocument CreatePrefabWithColliders()
    {
        var root = new Object3DNodeDefinition(Guid.NewGuid().ToString("D"), "Root",
            [Object3DComponentDefinition.CreateTransform(), Object3DComponentDefinition.CreateModel(new("model.glb", 1, "Y")),
                Object3DComponentDefinition.CreateCollider(new(Object3DColliderKind.Box, Center: [0, 0, 0], Size: [1, 2, 3], SourceFrame: Object3DColliderSourceFrame.ModelLocal))], []);
        root = root with { Transform = new(new(1, 0, 0), MachineVector3.Zero, MachineVector3.One) };
        var sphere = new Object3DNodeDefinition(Guid.NewGuid().ToString("D"), "Sphere",
            [Object3DComponentDefinition.CreateTransform(), Object3DComponentDefinition.CreateCollider(new(Object3DColliderKind.Sphere, Center: [0, 0, 0], Radius: .5, SourceFrame: Object3DColliderSourceFrame.NodeLocal, IsTrigger: true))], []);
        sphere = sphere with { Transform = new(new(0, 1, 0), MachineVector3.Zero, MachineVector3.One) };
        var capsule = new Object3DNodeDefinition(Guid.NewGuid().ToString("D"), "Capsule",
            [Object3DComponentDefinition.CreateTransform(), Object3DComponentDefinition.CreateCollider(new(Object3DColliderKind.Capsule, Center: [0, 0, 0], Radius: .5, Height: 2, Axis: Object3DCapsuleAxis.Y, SourceFrame: Object3DColliderSourceFrame.NodeLocal))], []);
        var mesh = new Object3DNodeDefinition(Guid.NewGuid().ToString("D"), "Mesh",
            [Object3DComponentDefinition.CreateTransform(), Object3DComponentDefinition.CreateModel(new("model.glb", 1, "Y")), Object3DComponentDefinition.CreateCollider(new(Object3DColliderKind.Mesh))], []);
        return Object3DDocument.Create("Fixture") with { Root = root with { Children = [sphere, capsule, mesh] } };
    }

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.InRange(MathF.Abs(expected.X - actual.X), 0, 1e-4f);
        Assert.InRange(MathF.Abs(expected.Y - actual.Y), 0, 1e-4f);
        Assert.InRange(MathF.Abs(expected.Z - actual.Z), 0, 1e-4f);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
