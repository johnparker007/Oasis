using NUnit.Framework;
using OasisPlayer.RuntimeBuild;
using UnityEngine;

public sealed class RuntimeReelMeshFactoryTests
{
    [Test]
    public void GeneratedCylinder_HasExpectedOpenSurfaceGeometry()
    {
        var mesh = RuntimeReelMeshFactory.Create(2f, 1f, 8);
        Assert.AreEqual(18, mesh.vertexCount);
        Assert.AreEqual(48, mesh.triangles.Length);
        Assert.AreEqual(new Bounds(Vector3.zero, new Vector3(2f, 2f, 2f)), mesh.bounds);
        foreach (var uv in mesh.uv)
        {
            Assert.GreaterOrEqual(uv.x, 0f);
            Assert.LessOrEqual(uv.x, 1f);
            Assert.GreaterOrEqual(uv.y, 0f);
            Assert.LessOrEqual(uv.y, 1f);
        }
        Assert.AreEqual(mesh.vertices[0], mesh.vertices[16]);
        Assert.AreEqual(mesh.vertices[1], mesh.vertices[17]);
        Assert.AreEqual(0f, mesh.uv[0].y);
        Assert.AreEqual(1f, mesh.uv[16].y);
    }

    [Test]
    public void GeneratedCylinder_HasOutwardNormalsAndWinding()
    {
        var mesh = RuntimeReelMeshFactory.Create(2f, 1f, 16);
        var vertices = mesh.vertices;
        var normals = mesh.normals;
        for (var i = 0; i < vertices.Length; i++)
        {
            var radial = new Vector3(0f, vertices[i].y, vertices[i].z).normalized;
            Assert.Greater(Vector3.Dot(radial, normals[i]), 0.99f);
        }
        var tris = mesh.triangles;
        for (var i = 0; i < tris.Length; i += 3)
        {
            var a = vertices[tris[i]];
            var b = vertices[tris[i + 1]];
            var c = vertices[tris[i + 2]];
            var faceNormal = Vector3.Cross(b - a, c - a).normalized;
            var center = ((a + b + c) / 3f);
            var radial = new Vector3(0f, center.y, center.z).normalized;
            Assert.Greater(Vector3.Dot(radial, faceNormal), 0f);
        }
    }
    [Test]
    public void MillimetresToMetres_ConvertsRuntimeManifestDimensionsOnce()
    {
        Assert.AreEqual(0.05f, RuntimeReelUnits.MillimetresToMetres(50f));
        Assert.AreEqual(0.105f, RuntimeReelUnits.MillimetresToMetres(105f));
    }

    [Test]
    public void GeneratedCylinder_UsesSpecifiedMetreDimensions()
    {
        var mesh = RuntimeReelMeshFactory.Create(RuntimeReelUnits.MillimetresToMetres(50f), RuntimeReelUnits.MillimetresToMetres(105f), 32);

        Assert.AreEqual(new Vector3(0.05f, 0.21f, 0.21f), mesh.bounds.size);
    }

    [Test]
    public void MeshCache_IncludesPhysicalDimensionsInKey()
    {
        var factory = new RuntimeReelMeshFactory();

        var standard = factory.Get(0.05f, 0.105f, 32);
        var standardAgain = factory.Get(0.05f, 0.105f, 32);
        var wide = factory.Get(0.075f, 0.150f, 32);

        Assert.AreSame(standard, standardAgain);
        Assert.AreNotSame(standard, wide);
        Assert.AreEqual(new Vector3(0.05f, 0.21f, 0.21f), standard.bounds.size);
        Assert.AreEqual(new Vector3(0.075f, 0.3f, 0.3f), wide.bounds.size);
    }

    [Test]
    public void FaceApertureDimensions_DoNotAffectGeneratedMeshDimensions()
    {
        var smallApertureMesh = RuntimeReelMeshFactory.Create(RuntimeReelUnits.MillimetresToMetres(50f), RuntimeReelUnits.MillimetresToMetres(105f), 32);
        var largeApertureMesh = RuntimeReelMeshFactory.Create(RuntimeReelUnits.MillimetresToMetres(50f), RuntimeReelUnits.MillimetresToMetres(105f), 32);

        Assert.AreEqual(smallApertureMesh.bounds.size, largeApertureMesh.bounds.size);
        Assert.AreEqual(new Vector3(0.05f, 0.21f, 0.21f), largeApertureMesh.bounds.size);
    }

}
