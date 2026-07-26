using NUnit.Framework;
using System.IO;
using OasisPlayer.RuntimeBuild;
using OasisPlayer.Loading;
using UnityEngine;

namespace OasisPlayer.Tests
{
    public sealed class RuntimeCabinetReflectionTests
    {
        [Test]
        public void PlaneValidationNormalizesBasisAndRejectsInvalidInputs()
        {
            Assert.True(RuntimeFaceReflectionPlane.TryCreate(Vector3.zero, Vector3.right * 2, Vector3.up * 3, 2, 1, out var plane));
            Assert.AreEqual(Vector3.forward, plane.NormalWS);
            Assert.False(RuntimeFaceReflectionPlane.TryCreate(Vector3.zero, Vector3.right, Vector3.up, 0, 1, out _));
            Assert.False(RuntimeFaceReflectionPlane.TryCreate(Vector3.zero, Vector3.right, Vector3.up, 1, 0, out _));
            Assert.False(RuntimeFaceReflectionPlane.TryCreate(Vector3.zero, Vector3.zero, Vector3.up, 1, 1, out _));
            Assert.False(RuntimeFaceReflectionPlane.TryCreate(Vector3.zero, Vector3.right, Vector3.right, 1, 1, out _));
            Assert.False(RuntimeFaceReflectionPlane.TryCreate(new Vector3(float.NaN, 0, 0), Vector3.right, Vector3.up, 1, 1, out _));
        }

        [Test]
        public void RayIntersectionRejectsParallelAndNonForwardHits()
        {
            RuntimeFaceReflectionPlane.TryCreate(Vector3.zero, Vector3.right, Vector3.up, 1, 1, out var plane);
            Assert.True(RuntimeCabinetReflectionMath.TryIntersectRayWithPlane(new Vector3(.5f, .5f, 1), Vector3.back, plane, out var distance, out var hit));
            Assert.AreEqual(1, distance, 1e-5f); Assert.AreEqual(new Vector3(.5f, .5f, 0), hit);
            Assert.False(RuntimeCabinetReflectionMath.TryIntersectRayWithPlane(Vector3.forward, Vector3.right, plane, out _, out _));
            Assert.False(RuntimeCabinetReflectionMath.TryIntersectRayWithPlane(Vector3.forward, new Vector3(1, 0, -1e-7f), plane, out _, out _));
            Assert.False(RuntimeCabinetReflectionMath.TryIntersectRayWithPlane(Vector3.forward, Vector3.forward, plane, out _, out _));
            Assert.False(RuntimeCabinetReflectionMath.TryIntersectRayWithPlane(Vector3.zero, Vector3.forward, plane, out _, out _));
        }

        [TestCase(0, 0)] [TestCase(1, 0)] [TestCase(0, 1)] [TestCase(1, 1)] [TestCase(.5f, .5f)]
        public void WorldPointMapsToExpectedUv(float x, float y)
        {
            RuntimeFaceReflectionPlane.TryCreate(new Vector3(2, 3, 4), Vector3.right, Vector3.up, 2, 4, out var plane);
            Assert.True(RuntimeCabinetReflectionMath.TryWorldPointToFaceUv(plane.OriginWS + Vector3.right * (x * 2) + Vector3.up * (y * 4), plane, out var uv));
            Assert.AreEqual(new Vector2(x, y), uv);
        }

        [Test]
        public void ReflectedUvChangesWithCabinetNormalAndRejectsAwayRay()
        {
            RuntimeFaceReflectionPlane.TryCreate(new Vector3(-1, -1, 2), Vector3.right, Vector3.up, 2, 2, out var plane);
            Assert.True(RuntimeCabinetReflectionMath.TryReflectToFaceUv(new Vector3(0, 0, 1), Vector3.zero, Vector3.forward, plane, out var flatUv));
            Assert.True(RuntimeCabinetReflectionMath.TryReflectToFaceUv(new Vector3(.2f, 0, 1), Vector3.zero, new Vector3(.1f, 0, 1), plane, out var bevelUv));
            Assert.That(Mathf.Abs(flatUv.x - bevelUv.x), Is.GreaterThan(1e-4f));
            Assert.False(RuntimeCabinetReflectionMath.TryReflectToFaceUv(Vector3.back, Vector3.zero, Vector3.forward, plane, out _));
        }

        [Test]
        public void CabinetShaderExposesSourceAndReflectionProperties()
        {
            var shader = Shader.Find(RuntimeCabinetReflectionShaderProperties.ShaderName); Assert.NotNull(shader);
            var material = new Material(shader);
            try
            {
                Assert.True(material.HasProperty(RuntimeFaceShaderProperties.ArtworkTexture));
                Assert.True(material.HasProperty(RuntimeFaceShaderProperties.MaskTexture));
                Assert.True(material.HasProperty(RuntimeFaceShaderProperties.LampIds0Texture));
                Assert.True(material.HasProperty(RuntimeFaceShaderProperties.LampWeights0Texture));
                Assert.True(material.HasProperty(RuntimeFaceShaderProperties.LampStateTexture));
                Assert.True(material.HasProperty(RuntimeCabinetReflectionShaderProperties.FaceOrigin));
                Assert.True(material.HasProperty(RuntimeCabinetReflectionShaderProperties.FaceSize));
                Assert.True(material.HasProperty(RuntimeCabinetReflectionShaderProperties.Strength));
                foreach (var property in RuntimeCabinetReflectionShaderProperties.RequiredProperties) Assert.True(material.HasProperty(property), property.ToString());
                Assert.True(material.HasProperty(RuntimeFaceShaderProperties.LampExposureStops));
                Assert.True(material.HasProperty(RuntimeFaceShaderProperties.MaskStrength));
                Assert.True(material.HasProperty(RuntimeFaceShaderProperties.FaceRotationQuarterTurns));
                Assert.True(material.HasProperty(RuntimeFaceShaderProperties.FaceFlipHorizontal));
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void BindingAssignsSharedLampTextureAndRestoresPropertyBlock()
        {
            var target = new GameObject("reflector");
            var material = new Material(Shader.Find(RuntimeCabinetReflectionShaderProperties.ShaderName));
            var textures = new[] { NewTexture(), NewTexture(), NewTexture(), NewTexture() };
            var machine = new RuntimeMachine(null, target);
            var renderer = target.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            var original = new MaterialPropertyBlock(); original.SetFloat(RuntimeCabinetReflectionShaderProperties.Strength, .73f); renderer.SetPropertyBlock(original, 0);
            machine.RegisterFace(new RuntimeFace(new MachineRuntimeFaceReference { faceId = "glass" }, null, null,
                new RuntimeTextureAsset("art", textures[0]), new RuntimeTextureAsset("mask", textures[1]), null,
                new RuntimeTextureAsset("ids", textures[2]), new RuntimeTextureAsset("weights", textures[3])));
            RuntimeFaceReflectionPlane.TryCreate(Vector3.zero, Vector3.right, Vector3.up, 1, 1, out var plane);
            try
            {
                Assert.True(RuntimeCabinetReflectionBinding.TryCreate(machine, "glass", renderer, 0, plane, out var binding, out var warning), warning);
                var actual = new MaterialPropertyBlock(); renderer.GetPropertyBlock(actual, 0);
                Assert.AreSame(machine.LampStateTexture.Texture, actual.GetTexture(RuntimeFaceShaderProperties.LampStateTexture));
                var lampTexture = actual.GetTexture(RuntimeFaceShaderProperties.LampStateTexture);
                machine.LampState.SetBrightness(1, .5f); machine.ApplyDynamicState();
                renderer.GetPropertyBlock(actual, 0); Assert.AreSame(lampTexture, actual.GetTexture(RuntimeFaceShaderProperties.LampStateTexture));
                Assert.False(RuntimeCabinetReflectionBinding.TryCreate(machine, "glass", renderer, 1, plane, out _, out _));
                machine.AddCabinetReflectionBinding(binding); machine.UnloadAssets(); machine.UnloadAssets();
                renderer.GetPropertyBlock(actual, 0); Assert.AreEqual(.73f, actual.GetFloat(RuntimeCabinetReflectionShaderProperties.Strength));
            }
            finally
            {
                machine.UnloadAssets();
                Object.DestroyImmediate(material); Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void CabinetLocalPlaneHandlesTranslationRotationNonUniformAndMirroredScale()
        {
            var root = new GameObject("root");
            try
            {
                root.transform.position = new Vector3(3, 4, 5); root.transform.rotation = Quaternion.Euler(0, 90, 0); root.transform.localScale = new Vector3(-2, 3, 4);
                var source = new RuntimeCabinetReflectionPlaneDefinition
                {
                    origin = new RuntimeCabinetReflectionVector { x = 1, y = 2, z = 0 },
                    right = new RuntimeCabinetReflectionVector { x = 1 }, up = new RuntimeCabinetReflectionVector { y = 1 }, width = 2, height = 1
                };
                Assert.True(RuntimeCabinetReflectionRenderer.TryWorldPlane(root.transform, source, out var plane));
                Assert.AreEqual(root.transform.TransformPoint(new Vector3(1, 2, 0)), plane.OriginWS);
                Assert.AreEqual(4, plane.Width, 1e-5f); Assert.AreEqual(3, plane.Height, 1e-5f);
                Assert.AreEqual(Vector3.Cross(plane.RightWS, plane.UpWS).normalized, plane.NormalWS);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void LookupAndLampStateTexturesUsePointClampSampling()
        {
            var state = new RuntimeLampState(); using (var stateTexture = new RuntimeLampStateTexture(state))
            {
                Assert.AreEqual(FilterMode.Point, stateTexture.Texture.filterMode); Assert.AreEqual(TextureWrapMode.Clamp, stateTexture.Texture.wrapMode);
            }
            var path = Path.Combine(Application.temporaryCachePath, "oasis-reflection-lookup.png");
            var source = NewTexture(); File.WriteAllBytes(path, source.EncodeToPNG()); Object.DestroyImmediate(source);
            try
            {
                Assert.True(new PngRuntimeTextureAssetLoader().TryLoad(path, RuntimeTextureRole.LookupData, out var asset, out var error), error);
                Assert.AreEqual(FilterMode.Point, asset.Texture.filterMode); Assert.AreEqual(TextureWrapMode.Clamp, asset.Texture.wrapMode); asset.Unload();
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Test]
        public void ShaderSourceRetainsSharedReconstructionBoundsAndProductionPasses()
        {
            var source = File.ReadAllText("Assets/_Project/Shaders/OasisCabinetAnalyticReflection.shader");
            StringAssert.Contains("#include \"Includes/OasisFaceLampCommon.hlsl\"", source);
            StringAssert.Contains("art.rgb*_OasisReflectionUnlitArtworkStrength", source);
            StringAssert.Contains("_OasisReflectionLitLampStrength", source);
            StringAssert.Contains("ReconstructFace(saturate(uv+d))", source);
            StringAssert.Contains("UsePass \"Universal Render Pipeline/Lit/ShadowCaster\"", source);
            StringAssert.Contains("UsePass \"Universal Render Pipeline/Lit/DepthOnly\"", source);
            StringAssert.Contains("UsePass \"Universal Render Pipeline/Lit/DepthNormals\"", source);
            StringAssert.Contains("UsePass \"Universal Render Pipeline/Lit/Meta\"", source);
        }

        private static Texture2D NewTexture() { return new Texture2D(1, 1, TextureFormat.RGBA32, false, true); }
    }
}
