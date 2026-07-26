using System;
using NUnit.Framework;
using OasisPlayer.RuntimeBuild;
using UnityEngine;

namespace OasisPlayer.Tests
{
    public sealed class RuntimeCabinetReflectionTests
    {
        private static readonly RuntimeCabinetReflectionPlane FacePlane = new RuntimeCabinetReflectionPlane(
            new Vector3(-1f, -1f, 0f), Vector3.right, Vector3.up, Vector3.forward, 2f, 2f);

        [Test]
        public void ReflectedRayHitsFacePlaneAndReturnsUntransformedUv()
        {
            Assert.True(RuntimeCabinetReflectionMath.TryIntersectReflectedRay(
                new Vector3(0f, 0f, -2f), new Vector3(0f, 0f, -1f), Vector3.right, FacePlane, out var uv, out var distance));

            Assert.AreEqual(new Vector2(0.5f, 0.5f), uv);
            Assert.AreEqual(1f, distance, 0.0001f);
        }

        [Test]
        public void ReflectedRayRejectsParallelBehindAndOutsideHits()
        {
            Assert.False(RuntimeCabinetReflectionMath.TryIntersectReflectedRay(
                new Vector3(-2f, 0f, 0f), Vector3.zero, Vector3.up, FacePlane, out _, out _));
            Assert.False(RuntimeCabinetReflectionMath.TryIntersectReflectedRay(
                new Vector3(0f, 0f, -2f), new Vector3(0f, 0f, -1f), Vector3.forward, FacePlane, out _, out _));
            Assert.False(RuntimeCabinetReflectionMath.TryIntersectReflectedRay(
                new Vector3(4f, 0f, -2f), new Vector3(4f, 0f, -1f), Vector3.right, FacePlane, out _, out _));
        }

        [Test]
        public void FacePlaneRequiresPositiveDimensionsAndConsistentBasis()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeCabinetReflectionPlane(Vector3.zero, Vector3.right, Vector3.up, Vector3.forward, 0f, 1f));
            Assert.Throws<ArgumentException>(() => new RuntimeCabinetReflectionPlane(Vector3.zero, Vector3.right, Vector3.right, Vector3.forward, 1f, 1f));
            Assert.Throws<ArgumentException>(() => new RuntimeCabinetReflectionPlane(Vector3.zero, Vector3.right, Vector3.up, Vector3.back, 1f, 1f));
        }

        [Test]
        public void CabinetShaderExposesFaceSourceAndPlaneProperties()
        {
            var shader = Shader.Find(RuntimeCabinetReflectionShaderProperties.ShaderName);
            Assert.NotNull(shader);
            var material = new Material(shader);
            try
            {
                Assert.True(material.HasProperty(RuntimeFaceShaderProperties.ArtworkTexture));
                Assert.True(material.HasProperty(RuntimeFaceShaderProperties.MaskTexture));
                Assert.True(material.HasProperty(RuntimeFaceShaderProperties.LampIds0Texture));
                Assert.True(material.HasProperty(RuntimeFaceShaderProperties.LampWeights0Texture));
                Assert.True(material.HasProperty(RuntimeFaceShaderProperties.LampStateTexture));
                Assert.True(material.HasProperty(RuntimeFaceShaderProperties.LampExposureStops));
                Assert.True(material.HasProperty(RuntimeFaceShaderProperties.MaskStrength));
                Assert.True(material.HasProperty(RuntimeFaceShaderProperties.FaceRotationQuarterTurns));
                Assert.True(material.HasProperty(RuntimeFaceShaderProperties.FaceFlipHorizontal));
                Assert.True(material.HasProperty(RuntimeCabinetReflectionShaderProperties.FacePlaneOrigin));
                Assert.True(material.HasProperty(RuntimeCabinetReflectionShaderProperties.FacePlaneSize));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }
    }
}
