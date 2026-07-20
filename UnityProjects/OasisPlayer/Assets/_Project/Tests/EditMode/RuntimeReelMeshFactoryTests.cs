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
}
