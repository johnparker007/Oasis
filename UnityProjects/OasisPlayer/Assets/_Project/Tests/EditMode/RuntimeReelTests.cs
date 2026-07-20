using NUnit.Framework;
using OasisPlayer.RuntimeBuild;
using UnityEngine;

namespace OasisPlayer.Tests.EditMode
{
    public sealed class RuntimeReelTests
    {
        [Test]
        public void GeneratedCylinderMesh_HasExpectedOpenWrappedGeometry()
        {
            var mesh = RuntimeReelMeshFactory.GetOrCreate(2f, 1f, 8);

            Assert.AreEqual(18, mesh.vertexCount);
            Assert.AreEqual(48, mesh.triangles.Length);
            Assert.AreEqual(new Vector3(0f, 0f, 0f), mesh.bounds.center);
            Assert.AreEqual(new Vector3(2f, 2f, 2f), mesh.bounds.size);
            foreach (var uv in mesh.uv)
            {
                Assert.GreaterOrEqual(uv.x, 0f);
                Assert.LessOrEqual(uv.x, 1f);
                Assert.GreaterOrEqual(uv.y, 0f);
                Assert.LessOrEqual(uv.y, 1f);
            }
            Assert.AreEqual(mesh.vertices[0].y, mesh.vertices[16].y, 0.0001f);
            Assert.AreEqual(mesh.vertices[0].z, mesh.vertices[16].z, 0.0001f);
            Assert.AreEqual(0f, mesh.uv[0].y, 0.0001f);
            Assert.AreEqual(1f, mesh.uv[16].y, 0.0001f);
        }

        [TestCase(0f, 0f)]
        [TestCase(24f, -90f)]
        [TestCase(48f, -180f)]
        [TestCase(72f, 90f)]
        [TestCase(96f, 0f)]
        [TestCase(-1f, 3.75f)]
        [TestCase(120f, -90f)]
        public void PositionConverter_WrapsCanonicalPosition(float position, float expectedX)
        {
            var actual = RuntimeReelPositionConverter.ToLocalRotation(position, false, 0f).eulerAngles.x;
            Assert.AreEqual(Normalize(expectedX), actual, 0.001f);
        }

        [Test]
        public void PositionConverter_AppliesReversalAndBandOffset()
        {
            var reversed = RuntimeReelPositionConverter.ToLocalRotation(24f, true, 0f).eulerAngles.x;
            var offset = RuntimeReelPositionConverter.ToLocalRotation(0f, false, 0.25f).eulerAngles.x;
            Assert.AreEqual(90f, reversed, 0.001f);
            Assert.AreEqual(270f, offset, 0.001f);
        }

        private static float Normalize(float value)
        {
            return ((value % 360f) + 360f) % 360f;
        }
    }
}
