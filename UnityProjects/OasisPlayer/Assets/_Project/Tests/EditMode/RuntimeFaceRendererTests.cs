using NUnit.Framework;
using OasisPlayer.RuntimeBuild;
using UnityEngine;

namespace OasisPlayer.Tests
{
    public sealed class RuntimeFaceRendererTests
    {
        [Test]
        public void ValidRuntimeFaceProducesBindingAndDoesNotMutateSharedMaterial()
        {
            var target = new GameObject("OasisFace_Front");
            var sharedMaterial = new Material(Shader.Find("Standard"));
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            var mask = new Texture2D(1, 1, TextureFormat.RGBA32, false);

            try
            {
                var renderer = target.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = sharedMaterial;
                var face = CreateFace(target.transform, texture, mask);
                var sut = new RuntimeFaceRenderer(new RuntimeFaceMaterialFactory());
                var machine = CreateMachine(face);

                var rendered = sut.TryRender(machine, face, out var warning);

                Assert.True(rendered, warning);
                Assert.NotNull(face.RenderBinding);
                Assert.AreSame(renderer, face.RenderBinding.Renderer);
                Assert.AreSame(sharedMaterial, face.RenderBinding.OriginalMaterials[0]);
                Assert.AreNotSame(sharedMaterial, renderer.sharedMaterials[0]);
                var runtimeMaterial = renderer.sharedMaterials[0];
                Assert.AreEqual(RuntimeFaceShaderProperties.ShaderName, runtimeMaterial.shader.name);
                Assert.AreSame(texture, runtimeMaterial.GetTexture(RuntimeFaceShaderProperties.ArtworkTexture));
                Assert.AreSame(mask, runtimeMaterial.GetTexture(RuntimeFaceShaderProperties.MaskTexture));
                Assert.AreEqual(Vector2.one, runtimeMaterial.GetTextureScale(RuntimeFaceShaderProperties.ArtworkTexture));
                Assert.AreEqual(Vector2.zero, runtimeMaterial.GetTextureOffset(RuntimeFaceShaderProperties.ArtworkTexture));
                Assert.AreEqual(Vector2.one, runtimeMaterial.GetTextureScale(RuntimeFaceShaderProperties.MaskTexture));
                Assert.AreEqual(Vector2.zero, runtimeMaterial.GetTextureOffset(RuntimeFaceShaderProperties.MaskTexture));
                Assert.AreEqual((int)UnityEngine.Rendering.CullMode.Front, runtimeMaterial.GetInt(RuntimeFaceShaderProperties.CullMode));
                Assert.AreEqual(-1f, runtimeMaterial.GetFloat(RuntimeFaceShaderProperties.NormalSign));
                Assert.AreEqual(0, runtimeMaterial.GetInt(RuntimeFaceShaderProperties.FaceRotationQuarterTurns));
                Assert.AreEqual(0f, runtimeMaterial.GetFloat(RuntimeFaceShaderProperties.FaceFlipHorizontal));
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(sharedMaterial);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(mask);
            }
        }

        [Test]
        public void TargetWithoutRendererReportsWarning()
        {
            var target = new GameObject("OasisFace_Front");
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            var mask = new Texture2D(1, 1, TextureFormat.RGBA32, false);

            try
            {
                var face = CreateFace(target.transform, texture, mask);
                var sut = new RuntimeFaceRenderer(new RuntimeFaceMaterialFactory());
                var machine = CreateMachine(face);

                var rendered = sut.TryRender(machine, face, out var warning);

                Assert.False(rendered);
                Assert.That(warning, Does.Contain("has no usable renderer"));
                Assert.Null(face.RenderBinding);
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(mask);
            }
        }

        [Test]
        public void MultipleFacesRenderIndependentlyAndCleanupRestoresOriginalMaterials()
        {
            var first = CreateTarget("OasisFace_First");
            var second = CreateTarget("OasisFace_Second");
            var firstOriginal = first.GetComponent<MeshRenderer>().sharedMaterial;
            var secondOriginal = second.GetComponent<MeshRenderer>().sharedMaterial;
            var firstTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            var secondTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            var firstMask = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            var secondMask = new Texture2D(1, 1, TextureFormat.RGBA32, false);

            try
            {
                var sut = new RuntimeFaceRenderer(new RuntimeFaceMaterialFactory());
                var firstFace = CreateFace(first.transform, firstTexture, firstMask, "first");
                var secondFace = CreateFace(second.transform, secondTexture, secondMask, "second");

                var firstMachine = CreateMachine(firstFace);
                var secondMachine = CreateMachine(secondFace);
                Assert.True(sut.TryRender(firstMachine, firstFace, out var firstWarning), firstWarning);
                Assert.True(sut.TryRender(secondMachine, secondFace, out var secondWarning), secondWarning);
                Assert.AreNotSame(first.GetComponent<MeshRenderer>().sharedMaterial, second.GetComponent<MeshRenderer>().sharedMaterial);

                firstFace.RenderBinding.Dispose();
                secondFace.RenderBinding.Dispose();

                Assert.AreSame(firstOriginal, first.GetComponent<MeshRenderer>().sharedMaterial);
                Assert.AreSame(secondOriginal, second.GetComponent<MeshRenderer>().sharedMaterial);
            }
            finally
            {
                Object.DestroyImmediate(firstOriginal);
                Object.DestroyImmediate(secondOriginal);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(firstTexture);
                Object.DestroyImmediate(secondTexture);
                Object.DestroyImmediate(firstMask);
                Object.DestroyImmediate(secondMask);
            }
        }

        [Test]
        public void AmbiguousMaterialSlotsWarnWithoutReplacingMaterials()
        {
            var target = CreateTarget("OasisFace_Front");
            var renderer = target.GetComponent<MeshRenderer>();
            var first = renderer.sharedMaterial;
            var second = new Material(Shader.Find("Standard"));
            renderer.sharedMaterials = new[] { first, second };
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            var mask = new Texture2D(1, 1, TextureFormat.RGBA32, false);

            try
            {
                var face = CreateFace(target.transform, texture, mask);
                var sut = new RuntimeFaceRenderer(new RuntimeFaceMaterialFactory());
                var machine = CreateMachine(face);

                var rendered = sut.TryRender(machine, face, out var warning);

                Assert.False(rendered);
                Assert.That(warning, Does.Contain("material slots"));
                Assert.AreSame(first, renderer.sharedMaterials[0]);
                Assert.AreSame(second, renderer.sharedMaterials[1]);
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(mask);
            }
        }


        [Test]
        public void InvertedFrontSideConfiguresOwnedMaterialCullAndNormalSign()
        {
            var target = CreateTarget("OasisFace_Inverted");
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            var mask = new Texture2D(1, 1, TextureFormat.RGBA32, false);

            try
            {
                var face = CreateFace(target.transform, texture, mask, "inverted");
                face.Reference.frontSide = RuntimeFaceFrontSideExtensions.InvertedValue;
                face.Reference.faceRotation = 270;
                face.Reference.faceFlipHorizontal = true;
                var sut = new RuntimeFaceRenderer(new RuntimeFaceMaterialFactory());

                Assert.True(sut.TryRender(CreateMachine(face), face, out var warning), warning);

                var runtimeMaterial = face.RenderBinding.RuntimeMaterial;
                Assert.AreEqual((int)UnityEngine.Rendering.CullMode.Back, runtimeMaterial.GetInt(RuntimeFaceShaderProperties.CullMode));
                Assert.AreEqual(1f, runtimeMaterial.GetFloat(RuntimeFaceShaderProperties.NormalSign));
                Assert.AreEqual(2, runtimeMaterial.GetInt(RuntimeFaceShaderProperties.FaceRotationQuarterTurns));
                Assert.AreEqual(1f, runtimeMaterial.GetFloat(RuntimeFaceShaderProperties.FaceFlipHorizontal));
            }
            finally
            {
                Object.DestroyImmediate(target.GetComponent<MeshRenderer>().sharedMaterial);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(mask);
            }
        }



        [TestCase(0, 1)]
        [TestCase(90, 0)]
        [TestCase(180, 3)]
        [TestCase(270, 2)]
        [TestCase(450, 1)]
        public void TextureOrientationSeparatesEditorRotationFromUnityUvQuarterTurns(int editorRotation, int unityQuarterTurns)
        {
            var orientation = new RuntimeFaceTextureOrientation(editorRotation, false);

            Assert.AreEqual(editorRotation == 90 || editorRotation == 180 || editorRotation == 270 ? editorRotation : 0, orientation.EditorRotationDegrees);
            Assert.AreEqual(unityQuarterTurns, orientation.UnityUvQuarterTurns);
        }

        [Test]
        public void TextureOrientationTransformsUvCornersLikeEditorPreview()
        {
            var uv00 = new Vector2(0f, 0f);
            var uv10 = new Vector2(1f, 0f);
            var uv11 = new Vector2(1f, 1f);
            var uv01 = new Vector2(0f, 1f);

            AssertCorners(new RuntimeFaceTextureOrientation(0, false), uv10, uv11, uv01, uv00);
            AssertCorners(new RuntimeFaceTextureOrientation(90, false), uv00, uv10, uv11, uv01);
            AssertCorners(new RuntimeFaceTextureOrientation(180, false), uv01, uv00, uv10, uv11);
            AssertCorners(new RuntimeFaceTextureOrientation(270, false), uv11, uv01, uv00, uv10);
            AssertCorners(new RuntimeFaceTextureOrientation(0, true), uv00, uv01, uv11, uv10);
            AssertCorners(new RuntimeFaceTextureOrientation(90, true), uv10, uv00, uv01, uv11);
            AssertCorners(new RuntimeFaceTextureOrientation(180, true), uv11, uv10, uv00, uv01);
            AssertCorners(new RuntimeFaceTextureOrientation(270, true), uv01, uv11, uv10, uv00);
        }

        private static void AssertCorners(RuntimeFaceTextureOrientation orientation, Vector2 source0, Vector2 source1, Vector2 source2, Vector2 source3)
        {
            Assert.AreEqual(source0, orientation.TransformUv(new Vector2(0f, 0f)));
            Assert.AreEqual(source1, orientation.TransformUv(new Vector2(1f, 0f)));
            Assert.AreEqual(source2, orientation.TransformUv(new Vector2(1f, 1f)));
            Assert.AreEqual(source3, orientation.TransformUv(new Vector2(0f, 1f)));
        }

        private static GameObject CreateTarget(string name)
        {
            var target = new GameObject(name);
            var renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Standard"));
            target.AddComponent<MeshFilter>();
            return target;
        }

        private static RuntimeMachine CreateMachine(RuntimeFace face)
        {
            var machine = new RuntimeMachine(new ResolvedRuntimeBuild(string.Empty, new MachineRuntimeManifest(), string.Empty, new CabinetRuntimeManifest(), string.Empty, new MachineRuntimeFaceReference[0]), null);
            machine.RegisterFace(face);
            return machine;
        }

        private static RuntimeFace CreateFace(Transform target, Texture2D texture, Texture2D mask, string id = "front")
        {
            return new RuntimeFace(
                new MachineRuntimeFaceReference { faceId = id, cabinetFaceTargetId = "front" },
                new FaceRuntimeManifest { schemaVersion = 2, faceId = id, width = 1, height = 1, artwork = "artwork.png", mask = "mask.png" },
                target,
                new RuntimeTextureAsset("artwork.png", texture),
                new RuntimeTextureAsset("mask.png", mask));
        }


        [Test]
        public void LookupTexturesAreBoundWhenRuntimeFaceOwnsThem()
        {
            var target = CreateTarget("OasisFace_Front");
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            var mask = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            var trayId = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            var lampIds0 = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            var lampWeights0 = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);

            try
            {
                var face = new RuntimeFace(
                    new MachineRuntimeFaceReference { faceId = "front", cabinetFaceTargetId = "front" },
                    new FaceRuntimeManifest { schemaVersion = 2, faceId = "front", width = 1, height = 1, artwork = "artwork.png", mask = "mask.png", trayId = "trayId.png", lampIds0 = "lampIds0.png", lampWeights0 = "lampWeights0.png" },
                    target.transform,
                    new RuntimeTextureAsset("artwork.png", texture),
                    new RuntimeTextureAsset("mask.png", mask),
                    new RuntimeTextureAsset("trayId.png", trayId),
                    new RuntimeTextureAsset("lampIds0.png", lampIds0),
                    new RuntimeTextureAsset("lampWeights0.png", lampWeights0));

                var sut = new RuntimeFaceRenderer(new RuntimeFaceMaterialFactory());
                Assert.True(sut.TryRender(CreateMachine(face), face, out var warning), warning);
                var runtimeMaterial = face.RenderBinding.RuntimeMaterial;

                Assert.AreSame(trayId, runtimeMaterial.GetTexture(RuntimeFaceShaderProperties.TrayIdTexture));
                Assert.AreSame(lampIds0, runtimeMaterial.GetTexture(RuntimeFaceShaderProperties.LampIds0Texture));
                Assert.AreSame(lampWeights0, runtimeMaterial.GetTexture(RuntimeFaceShaderProperties.LampWeights0Texture));
                Assert.AreEqual(Vector2.one, runtimeMaterial.GetTextureScale(RuntimeFaceShaderProperties.LampIds0Texture));
                Assert.AreEqual(Vector2.zero, runtimeMaterial.GetTextureOffset(RuntimeFaceShaderProperties.LampIds0Texture));
            }
            finally
            {
                Object.DestroyImmediate(target.GetComponent<MeshRenderer>().sharedMaterial);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(mask);
                Object.DestroyImmediate(trayId);
                Object.DestroyImmediate(lampIds0);
                Object.DestroyImmediate(lampWeights0);
            }
        }

        [Test]
        public void MissingDedicatedShaderWarnsWithoutReplacingMaterial()
        {
            var target = CreateTarget("OasisFace_Front");
            var original = target.GetComponent<MeshRenderer>().sharedMaterial;
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            var mask = new Texture2D(1, 1, TextureFormat.RGBA32, false);

            try
            {
                var face = CreateFace(target.transform, texture, mask);
                var sut = new RuntimeFaceRenderer(new RuntimeFaceMaterialFactory("Oasis/MissingFaceShaderForTest"));

                var rendered = sut.TryRender(CreateMachine(face), face, out var warning);

                Assert.False(rendered);
                Assert.That(warning, Does.Contain("dedicated"));
                Assert.AreSame(original, target.GetComponent<MeshRenderer>().sharedMaterial);
                Assert.Null(face.RenderBinding);
            }
            finally
            {
                Object.DestroyImmediate(original);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(mask);
            }
        }


        [Test]
        public void MaterialCreationFailsWhenOrientationPropertiesAreMissing()
        {
            var target = CreateTarget("OasisFace_Front");
            var original = target.GetComponent<MeshRenderer>().sharedMaterial;
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            var mask = new Texture2D(1, 1, TextureFormat.RGBA32, false);

            try
            {
                var face = CreateFace(target.transform, texture, mask);
                var sut = new RuntimeFaceRenderer(new RuntimeFaceMaterialFactory("Oasis/FaceMissingOrientationProperties"));

                var rendered = sut.TryRender(CreateMachine(face), face, out var warning);

                Assert.False(rendered);
                Assert.That(warning, Does.Contain("required property"));
                Assert.AreSame(original, target.GetComponent<MeshRenderer>().sharedMaterial);
                Assert.Null(face.RenderBinding);
            }
            finally
            {
                Object.DestroyImmediate(original);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(mask);
            }
        }

        [Test]
        public void DynamicStateSeamCanMarkAndApplyPendingState()
        {
            var target = CreateTarget("OasisFace_Front");
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            var mask = new Texture2D(1, 1, TextureFormat.RGBA32, false);

            try
            {
                var face = CreateFace(target.transform, texture, mask);
                var sut = new RuntimeFaceRenderer(new RuntimeFaceMaterialFactory());
                Assert.True(sut.TryRender(CreateMachine(face), face, out var warning), warning);

                face.RenderBinding.MarkDynamicStateDirty();
                Assert.True(face.RenderBinding.HasDynamicState);
                face.RenderBinding.ApplyDynamicState();
                Assert.False(face.RenderBinding.HasDynamicState);
            }
            finally
            {
                if (target != null) Object.DestroyImmediate(target);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(mask);
            }
        }
    }
}
