using NUnit.Framework;
using System.IO;
using System.Text.RegularExpressions;
using OasisPlayer.RuntimeBuild;
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

        [TestCase(0, false, .1f, .2f, .1f, .2f)]
        [TestCase(90, false, .1f, .2f, .8f, .1f)]
        [TestCase(180, false, .9f, .2f, .1f, .8f)]
        [TestCase(270, false, .1f, .8f, .8f, .9f)]
        [TestCase(90, true, .9f, .8f, .8f, .9f)]
        public void ReflectionUsesNormalFaceTextureOrientation(int rotation, bool flip, float u, float v, float expectedU, float expectedV)
        {
            var orientation = new RuntimeFaceTextureOrientation(rotation, flip);
            Assert.AreEqual(new Vector2(expectedU, expectedV), orientation.TransformUv(new Vector2(u, v)));
        }

        [TestCase(0, 0, 1, 1)]
        [TestCase(1, 0, 0, 1)]
        [TestCase(0, 1, 1, 0)]
        [TestCase(1, 1, 0, 0)]
        [TestCase(.1f, .2f, .9f, .8f)]
        [TestCase(.9f, .2f, .1f, .8f)]
        [TestCase(.1f, .8f, .9f, .2f)]
        [TestCase(.9f, .8f, .1f, .2f)]
        public void ReflectionPlaneUvMapsToEstablishedRuntimeMeshUv(float u, float v, float expectedU, float expectedV)
        {
            Assert.AreEqual(new Vector2(expectedU, expectedV), RuntimeFaceBaseUvConversion.ConvertReflectionPlaneUvToUnityBaseUv(new Vector2(u, v)));
        }

        [TestCase(0, false)] [TestCase(90, false)] [TestCase(180, false)] [TestCase(270, false)]
        [TestCase(0, true)] [TestCase(90, true)] [TestCase(180, true)] [TestCase(270, true)]
        public void ReflectionFinalUvMatchesNormalFaceForEveryOrientation(int rotation, bool flipHorizontal)
        {
            var rawGlbPoints = new[]
            {
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(.1f, .2f), new Vector2(.9f, .2f), new Vector2(.1f, .8f), new Vector2(.9f, .8f)
            };
            var orientation = new RuntimeFaceTextureOrientation(rotation, flipHorizontal);
            foreach (var rawGlbUv in rawGlbPoints)
            {
                var reflectionFinalUv = orientation.TransformUv(RuntimeFaceBaseUvConversion.ConvertReflectionPlaneUvToUnityBaseUv(rawGlbUv));
                Assert.AreEqual(ExpectedFinalUv(rawGlbUv, rotation, flipHorizontal), reflectionFinalUv, $"rawGlbUv={rawGlbUv}, rotation={rotation}, flipHorizontal={flipHorizontal}");
            }
        }

        [TestCase(270, false, RuntimeFaceFrontSide.Inverted)]
        [TestCase(90, true, RuntimeFaceFrontSide.Normal)]
        public void VogueFaceUvParityIsIndependentOfFrontSide(int rotation, bool flipHorizontal, RuntimeFaceFrontSide frontSide)
        {
            var actual = new RuntimeFaceTextureOrientation(rotation, flipHorizontal).TransformUv(RuntimeFaceBaseUvConversion.ConvertReflectionPlaneUvToUnityBaseUv(new Vector2(.1f, .2f)));
            var expected = rotation == 270 ? new Vector2(.1f, .2f) : new Vector2(.1f, .8f);
            Assert.AreEqual(expected, actual, $"A left-side sample moved sides for {frontSide}.");
        }

        private static Vector2 ExpectedFinalUv(Vector2 rawPlaneUv, int editorRotation, bool flipHorizontal)
        {
            // Independent reference: the established Vogue vertex correspondence is (1-u,1-v),
            // followed by the documented Editor-to-Unity quarter-turn mapping.
            var uv = new Vector2(1f - rawPlaneUv.x, 1f - rawPlaneUv.y);
            var turns = (1 - editorRotation / 90 + 4) % 4;
            if (turns == 1) uv = new Vector2(1f - uv.y, uv.x);
            else if (turns == 2) uv = new Vector2(1f - uv.x, 1f - uv.y);
            else if (turns == 3) uv = new Vector2(uv.y, 1f - uv.x);
            return flipHorizontal ? new Vector2(1f - uv.x, uv.y) : uv;
        }

        [Test]
        public void SourceFrontSideChangesIntersectionSideWithoutChangingUvBasis()
        {
            RuntimeFaceReflectionPlane.TryCreate(Vector3.zero, Vector3.right, Vector3.up, 1, 1, out var plane);
            var normal = new[] { new RuntimeCabinetReflectionSource("face", plane, RuntimeFaceFrontSide.Normal) };
            var inverted = new[] { new RuntimeCabinetReflectionSource("face", plane, RuntimeFaceFrontSide.Inverted) };
            Assert.AreEqual(Vector3.back, normal[0].VisibleNormalWS);
            Assert.AreEqual(Vector3.forward, inverted[0].VisibleNormalWS);
            Assert.True(RuntimeCabinetReflectionMath.TrySelectNearest(new Vector3(.1f, .2f, -1), Vector3.forward, normal, out _, out var normalUv));
            Assert.False(RuntimeCabinetReflectionMath.TrySelectNearest(new Vector3(.1f, .2f, 1), Vector3.back, normal, out _, out _));
            Assert.True(RuntimeCabinetReflectionMath.TrySelectNearest(new Vector3(.1f, .2f, 1), Vector3.back, inverted, out _, out var invertedUv));
            Assert.False(RuntimeCabinetReflectionMath.TrySelectNearest(new Vector3(.1f, .2f, -1), Vector3.forward, inverted, out _, out _));
            Assert.AreEqual(normalUv, invertedUv);
        }

        [Test]
        public void ReflectionAndNormalFaceShareFrontSideNormalContract()
        {
            Assert.AreEqual(-1f, RuntimeFaceFrontSideOrientation.ResolveNormalSign(RuntimeFaceFrontSide.Normal));
            Assert.AreEqual(1f, RuntimeFaceFrontSideOrientation.ResolveNormalSign(RuntimeFaceFrontSide.Inverted));
            Assert.True(RuntimeCabinetReflectionMath.IsRayFacingVisibleSide(Vector3.forward, Vector3.back));
            Assert.False(RuntimeCabinetReflectionMath.IsRayFacingVisibleSide(Vector3.back, Vector3.back));
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
                Assert.True(material.HasProperty(RuntimeCabinetReflectionShaderProperties.Artwork[0]));
                Assert.True(material.HasProperty(RuntimeCabinetReflectionShaderProperties.Mask[0]));
                Assert.True(material.HasProperty(RuntimeCabinetReflectionShaderProperties.LampIds[0]));
                Assert.True(material.HasProperty(RuntimeCabinetReflectionShaderProperties.LampWeights[0]));
                Assert.True(material.HasProperty(RuntimeFaceShaderProperties.LampStateTexture));
                Assert.True(material.HasProperty(RuntimeCabinetReflectionShaderProperties.FaceOrigin[0]));
                Assert.True(material.HasProperty(RuntimeCabinetReflectionShaderProperties.FaceSize[0]));
                Assert.True(material.HasProperty(RuntimeCabinetReflectionShaderProperties.Strength));
                foreach (var property in RuntimeCabinetReflectionShaderProperties.RequiredProperties) Assert.True(material.HasProperty(property), property.ToString());
                Assert.True(material.HasProperty(RuntimeCabinetReflectionShaderProperties.Exposure[0]));
                Assert.True(material.HasProperty(RuntimeCabinetReflectionShaderProperties.MaskStrength[0]));
                Assert.True(material.HasProperty(RuntimeCabinetReflectionShaderProperties.Rotation[0]));
                Assert.True(material.HasProperty(RuntimeCabinetReflectionShaderProperties.Flip[0]));
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void BindingAssignsSharedLampTextureAndRestoresPropertyBlock()
        {
            var target = new GameObject("reflector");
            var material = new Material(Shader.Find("Standard"));
            var untouched = new Material(Shader.Find("Standard"));
            var baseTexture = NewTexture(); var normalTexture = NewTexture(); material.mainTexture = baseTexture; material.mainTextureScale = new Vector2(.5f, .75f); material.mainTextureOffset = new Vector2(.1f, .2f); material.color = new Color(.2f, .3f, .4f, 1); material.SetTexture("_BumpMap", normalTexture); material.SetFloat("_BumpScale", .8f); material.SetFloat("_Metallic", .6f); material.SetFloat("_Glossiness", .7f);
            var textures = new[] { NewTexture(), NewTexture(), NewTexture(), NewTexture() };
            var machine = new RuntimeMachine(null, target);
            var renderer = target.AddComponent<MeshRenderer>(); renderer.sharedMaterials = new[] { material, untouched };
            var original = new MaterialPropertyBlock(); original.SetFloat(RuntimeCabinetReflectionShaderProperties.Strength, .73f); renderer.SetPropertyBlock(original, 0);
            machine.RegisterFace(new RuntimeFace(new MachineRuntimeFaceReference { faceId = "glass" }, null, null,
                new RuntimeTextureAsset("art", textures[0]), new RuntimeTextureAsset("mask", textures[1]), null,
                new RuntimeTextureAsset("ids", textures[2]), new RuntimeTextureAsset("weights", textures[3])));
            RuntimeFaceReflectionPlane.TryCreate(Vector3.zero, Vector3.right, Vector3.up, 1, 1, out var plane);
            try
            {
                Assert.True(RuntimeCabinetReflectionBinding.TryCreate(machine, new[] { new RuntimeCabinetReflectionSource("glass", plane) }, renderer, 0, out var binding, out var warning), warning);
                Assert.AreSame(untouched, renderer.sharedMaterials[1]); Assert.AreNotSame(material, renderer.sharedMaterials[0]); Assert.AreSame(binding.RuntimeMaterial, renderer.sharedMaterials[0]);
                Assert.AreSame(baseTexture, binding.RuntimeMaterial.GetTexture("_BaseMap")); Assert.AreEqual(material.mainTextureScale, binding.RuntimeMaterial.GetTextureScale("_BaseMap")); Assert.AreEqual(material.mainTextureOffset, binding.RuntimeMaterial.GetTextureOffset("_BaseMap"));
                Assert.AreSame(normalTexture, binding.RuntimeMaterial.GetTexture("_BumpMap")); Assert.AreEqual(.8f, binding.RuntimeMaterial.GetFloat("_BumpScale"));
                Assert.AreEqual(material.color, binding.RuntimeMaterial.GetColor("_BaseColor")); Assert.AreEqual(.6f, binding.RuntimeMaterial.GetFloat("_Metallic")); Assert.AreEqual(.7f, binding.RuntimeMaterial.GetFloat("_Smoothness"));
                var ownedMaterial = binding.RuntimeMaterial;
                var actual = new MaterialPropertyBlock(); renderer.GetPropertyBlock(actual, 0);
                Assert.AreSame(machine.LampStateTexture.Texture, actual.GetTexture(RuntimeFaceShaderProperties.LampStateTexture));
                var lampTexture = actual.GetTexture(RuntimeFaceShaderProperties.LampStateTexture);
                machine.LampState.SetBrightness(1, .5f); machine.ApplyDynamicState();
                renderer.GetPropertyBlock(actual, 0); Assert.AreSame(lampTexture, actual.GetTexture(RuntimeFaceShaderProperties.LampStateTexture));
                Assert.False(RuntimeCabinetReflectionBinding.TryCreate(machine, new[] { new RuntimeCabinetReflectionSource("glass", plane) }, renderer, 2, out _, out _));
                machine.AddCabinetReflectionBinding(binding); machine.UnloadAssets(); machine.UnloadAssets();
                Assert.AreSame(material, renderer.sharedMaterials[0]); Assert.AreSame(untouched, renderer.sharedMaterials[1]);
                Assert.True(ownedMaterial == null);
                renderer.GetPropertyBlock(actual, 0); Assert.AreEqual(.73f, actual.GetFloat(RuntimeCabinetReflectionShaderProperties.Strength));
            }
            finally
            {
                machine.UnloadAssets();
                Object.DestroyImmediate(baseTexture); Object.DestroyImmediate(normalTexture); Object.DestroyImmediate(material); Object.DestroyImmediate(untouched); Object.DestroyImmediate(target);
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
        public void LampStateTextureUsesPointClampSampling()
        {
            var state = new RuntimeLampState(); using (var stateTexture = new RuntimeLampStateTexture(state))
            {
                Assert.AreEqual(FilterMode.Point, stateTexture.Texture.filterMode); Assert.AreEqual(TextureWrapMode.Clamp, stateTexture.Texture.wrapMode);
            }
        }

        [Test]
        public void ShaderSourceRetainsSharedReconstructionBoundsAndProductionPasses()
        {
            var source = File.ReadAllText("Assets/_Project/Shaders/OasisCabinetAnalyticReflection.shader");
            StringAssert.Contains("_OasisReflectionSourceCount", source);
            StringAssert.Contains("art.rgb*_OasisReflectionUnlitArtworkStrength", source);
            StringAssert.Contains("_OasisReflectionLitLampStrength", source);
            StringAssert.Contains("ReconstructFace(selected,saturate(uv+d))", source);
            StringAssert.Contains("TransformFaceUv(ConvertReflectionPlaneUvToUnityBaseUv(uv)", source);
            StringAssert.Contains("return 1.0 - uv;", File.ReadAllText("Assets/_Project/Shaders/Includes/OasisFaceUvOrientation.hlsl"));
            Assert.AreEqual(4, Regex.Matches(source, @"\bSAMPLER\(").Count, "Cabinet reflection shader must share Face samplers to remain below the D3D11 ps_4_0 limit.");
            StringAssert.DoesNotContain("sampler_OasisArtworkTex1", source);
            StringAssert.Contains("float3 colour=float3(0,0,0)", source);
            StringAssert.Contains("if(d>=-1e-5)return false", source, "Shader facing must use the same ray-dot-visible-normal < -epsilon convention as CPU selection.");
            StringAssert.Contains("UsePass \"Universal Render Pipeline/Lit/ShadowCaster\"", source);
            StringAssert.Contains("UsePass \"Universal Render Pipeline/Lit/DepthOnly\"", source);
            StringAssert.Contains("UsePass \"Universal Render Pipeline/Lit/DepthNormals\"", source);
            StringAssert.Contains("UsePass \"Universal Render Pipeline/Lit/Meta\"", source);
        }

        [TestCase("vogue/front/front_2", "vogue/front/front_2", true)]
        [TestCase("vogue/front/front_2", "Scene/vogue/front/front_2", true)]
        [TestCase("vogue/front/front_2", "Scene/other-vogue/front/front_2", false)]
        [TestCase("vogue/front/front_2", "OtherRoot/vogue/front/front_2", false)]
        public void RendererTargetResolutionUsesOnlyExactOrSyntheticScenePrefix(string authored, string runtimePath, bool expected)
        {
            var cabinet = new GameObject("cabinet");
            try
            {
                var expectedRenderer = CreateRendererPath(cabinet.transform, runtimePath);
                Assert.AreEqual(expected, RuntimeCabinetReflectionRenderer.TryResolveRenderer(cabinet, authored, out var actual, out _));
                Assert.AreSame(expected ? expectedRenderer : null, actual);
            }
            finally { Object.DestroyImmediate(cabinet); }
        }

        [Test]
        public void ExactRendererPathTakesPriorityOverSceneNormalizedPath()
        {
            var cabinet = new GameObject("cabinet");
            try
            {
                var exact = CreateRendererPath(cabinet.transform, "vogue/front/front_2");
                CreateRendererPath(cabinet.transform, "Scene/vogue/front/front_2");
                Assert.True(RuntimeCabinetReflectionRenderer.TryResolveRenderer(cabinet, "vogue/front/front_2", out var actual, out _));
                Assert.AreSame(exact, actual);
            }
            finally { Object.DestroyImmediate(cabinet); }
        }

        [Test]
        public void AmbiguousSceneNormalizedRendererPathsAreRejected()
        {
            var cabinet = new GameObject("cabinet");
            try
            {
                CreateRendererPath(cabinet.transform, "Scene/vogue/front/front_2");
                CreateRendererPath(cabinet.transform, "Scene/vogue/front/front_2");
                Assert.False(RuntimeCabinetReflectionRenderer.TryResolveRenderer(cabinet, "vogue/front/front_2", out var actual, out _));
                Assert.Null(actual);
            }
            finally { Object.DestroyImmediate(cabinet); }
        }

        private static Renderer CreateRendererPath(Transform root, string path)
        {
            var parent = root;
            foreach (var segment in path.Split('/')) { var child = new GameObject(segment); child.transform.SetParent(parent, false); parent = child.transform; }
            return parent.gameObject.AddComponent<MeshRenderer>();
        }

        private static Texture2D NewTexture() { return new Texture2D(1, 1, TextureFormat.RGBA32, false, true); }
    }
}
