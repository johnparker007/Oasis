using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using OasisPlayer.RuntimeBuild;

public sealed class RuntimeFacePlacementTests
{
    private const float Epsilon = 0.0005f;

    [Test]
    public void CenterAndCornersMapFromImageCoordinatesToQuad()
    {
        var target = CreateQuad("face", Vector3.zero, Quaternion.identity, new Vector3(2f, 3f, 1f));
        try
        {
            var face = CreateFace(target.transform, RuntimeFaceFrontSideExtensions.NormalValue, 200, 300);
            Assert.IsTrue(RuntimeFacePlacement.TryResolve(face, out var surface, out var warning), warning);
            AssertVector(Vector3.zero, surface.FacePointToWorld(100, 150, 200, 300));
            AssertVector(new Vector3(-1f, 1.5f, 0f), surface.FacePointToWorld(0, 0, 200, 300));
            AssertVector(new Vector3(1f, -1.5f, 0f), surface.FacePointToWorld(200, 300, 200, 300));
            AssertVector(Vector3.back, surface.VisibleNormal);
        }
        finally { Object.DestroyImmediate(target); }
    }

    [Test]
    public void RotatedTranslatedAndParentedTargetRespectsWorldTransformAndSize()
    {
        var parent = new GameObject("parent");
        var target = CreateQuad("face", new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f), new Vector3(4f, 2f, 1f));
        try
        {
            parent.transform.position = new Vector3(10f, 0f, 0f);
            parent.transform.rotation = Quaternion.Euler(0f, 0f, 30f);
            target.transform.SetParent(parent.transform, true);
            var face = CreateFace(target.transform, RuntimeFaceFrontSideExtensions.NormalValue, 400, 200);
            Assert.IsTrue(RuntimeFacePlacement.TryResolve(face, out var surface, out var warning), warning);
            Assert.AreEqual(4f, surface.PhysicalWidth, Epsilon);
            Assert.AreEqual(2f, surface.PhysicalHeight, Epsilon);
            AssertVector(target.transform.TransformPoint(Vector3.zero), surface.FacePointToWorld(200, 100, 400, 200));
            AssertVector(-Vector3.Cross(target.transform.right, target.transform.up).normalized, surface.VisibleNormal);
        }
        finally { Object.DestroyImmediate(target); Object.DestroyImmediate(parent); }
    }

    [Test]
    public void InvertedReversesVisibleNormal()
    {
        var target = CreateQuad("face", Vector3.zero, Quaternion.identity, Vector3.one);
        try
        {
            Assert.IsTrue(RuntimeFacePlacement.TryResolve(CreateFace(target.transform, RuntimeFaceFrontSideExtensions.InvertedValue, 100, 100), out var surface, out var warning), warning);
            AssertVector(Vector3.forward, surface.VisibleNormal);
        }
        finally { Object.DestroyImmediate(target); }
    }


    [Test]
    public void LocalXzFaceTargetMeshUsesNonDegenerateSurfaceDimensions()
    {
        var target = CreateLocalXzQuad("xzFace", 2f, 3f);
        try
        {
            var face = CreateFace(target.transform, RuntimeFaceFrontSideExtensions.NormalValue, 200, 300);
            Assert.IsTrue(RuntimeFacePlacement.TryResolve(face, out var surface, out var warning), warning);
            Assert.AreEqual(2f, surface.PhysicalWidth, Epsilon);
            Assert.AreEqual(3f, surface.PhysicalHeight, Epsilon);
            AssertVector(Vector3.zero, surface.FacePointToWorld(100, 150, 200, 300));
            AssertVector(new Vector3(-1f, 0f, 1.5f), surface.FacePointToWorld(0, 0, 200, 300));
        }
        finally { Object.DestroyImmediate(target); }
    }

    [Test]
    public void ReelRendererPlacesAxleBehindSurfaceAndAlignsAxle()
    {
        var target = CreateQuad("face", Vector3.zero, Quaternion.identity, new Vector3(2f, 2f, 1f));
        try
        {
            var face = CreateFace(target.transform, RuntimeFaceFrontSideExtensions.NormalValue, 200, 200, Reel("r1", 0.2f));
            face.Manifest.reels[0].BandTexture = new RuntimeTextureAsset("band", new Texture2D(2, 2));
            var machine = CreateMachine(face);
            new RuntimeReelRenderer().RenderReels(machine);
            Assert.AreEqual(1, face.ReelRenderBindings.Count);
            var reel = face.ReelRenderBindings[0].GameObject.transform;
            AssertVector(Vector3.right, reel.right);
            AssertVector(Vector3.back, reel.forward);
            Assert.AreEqual(0.21f, Vector3.Dot(Vector3.forward, reel.position), Epsilon);
            Assert.AreEqual(0.01f, Vector3.Dot(Vector3.forward, reel.position) - 0.2f, Epsilon);
            face.UnloadAssets();
            Assert.IsTrue(face.ReelRenderBindings.Count == 0);
            Assert.IsTrue(reel == null);
        }
        finally { Object.DestroyImmediate(target); }
    }

    [Test]
    public void DifferentReelRadiiKeepSameClearance()
    {
        var target = CreateQuad("face", Vector3.zero, Quaternion.identity, Vector3.one);
        try
        {
            var r1 = Reel("r1", 0.1f); var r2 = Reel("r2", 0.3f); r1.x = 25; r2.x = 75;
            r1.BandTexture = new RuntimeTextureAsset("b1", new Texture2D(2,2)); r2.BandTexture = new RuntimeTextureAsset("b2", new Texture2D(2,2));
            var face = CreateFace(target.transform, RuntimeFaceFrontSideExtensions.NormalValue, 100, 100, r1, r2);
            new RuntimeReelRenderer().RenderReels(CreateMachine(face));
            Assert.AreEqual(2, face.ReelRenderBindings.Count);
            Assert.AreEqual(0.01f, face.ReelRenderBindings[0].GameObject.transform.position.z - 0.1f, Epsilon);
            Assert.AreEqual(0.01f, face.ReelRenderBindings[1].GameObject.transform.position.z - 0.3f, Epsilon);
        }
        finally { Object.DestroyImmediate(target); }
    }

    [Test]
    public void MissingOrAmbiguousGeometryWarnsAndCreatesNoOriginFallback()
    {
        var target = new GameObject("empty");
        try
        {
            var reel = Reel("r", 0.2f); reel.BandTexture = new RuntimeTextureAsset("b", new Texture2D(2,2));
            var face = CreateFace(target.transform, RuntimeFaceFrontSideExtensions.NormalValue, 100, 100, reel);
            var machine = CreateMachine(face);
            new RuntimeReelRenderer().RenderReels(machine);
            Assert.AreEqual(0, face.ReelRenderBindings.Count);
            Assert.That(machine.Warnings[0], Does.Contain("no usable Face surface mesh"));
        }
        finally { Object.DestroyImmediate(target); }
    }

    private static GameObject CreateQuad(string name, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        go.transform.position = position;
        go.transform.rotation = rotation;
        go.transform.localScale = scale;
        return go;
    }


    private static GameObject CreateLocalXzQuad(string name, float width, float height)
    {
        var go = new GameObject(name);
        var mesh = new Mesh();
        var halfWidth = width * 0.5f;
        var halfHeight = height * 0.5f;
        mesh.vertices = new[]
        {
            new Vector3(-halfWidth, 0f, -halfHeight),
            new Vector3(halfWidth, 0f, -halfHeight),
            new Vector3(-halfWidth, 0f, halfHeight),
            new Vector3(halfWidth, 0f, halfHeight)
        };
        mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Standard"));
        return go;
    }

    private static RuntimeFace CreateFace(Transform target, string frontSide, int width, int height, params FaceRuntimeReelManifestEntry[] reels) =>
        new RuntimeFace(new MachineRuntimeFaceReference { faceId = "face", cabinetFaceTargetId = "target", frontSide = frontSide }, new FaceRuntimeManifest { faceId = "face", width = width, height = height, reels = reels }, target, new RuntimeTextureAsset("a", new Texture2D(2,2)), new RuntimeTextureAsset("m", new Texture2D(2,2)));

    private static FaceRuntimeReelManifestEntry Reel(string id, float radius) => new FaceRuntimeReelManifestEntry { objectId = id, machineReference = id, reelBand = "band", stops = 20, x = 40, y = 40, width = 20, height = 20, physicalWidth = 0.2f, physicalRadius = radius };

    private static RuntimeMachine CreateMachine(RuntimeFace face)
    {
        var machine = new RuntimeMachine(new ResolvedRuntimeBuild(string.Empty, new MachineRuntimeManifest(), string.Empty, new CabinetRuntimeManifest(), string.Empty, new MachineRuntimeFaceReference[0]), null);
        machine.RegisterFace(face);
        return machine;
    }

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.AreEqual(expected.x, actual.x, Epsilon);
        Assert.AreEqual(expected.y, actual.y, Epsilon);
        Assert.AreEqual(expected.z, actual.z, Epsilon);
    }
}
