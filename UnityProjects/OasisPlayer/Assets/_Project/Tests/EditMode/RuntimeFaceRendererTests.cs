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

                var rendered = sut.TryRender(face, out var warning);

                Assert.True(rendered, warning);
                Assert.NotNull(face.RenderBinding);
                Assert.AreSame(renderer, face.RenderBinding.Renderer);
                Assert.AreSame(sharedMaterial, face.RenderBinding.OriginalMaterials[0]);
                Assert.AreNotSame(sharedMaterial, renderer.sharedMaterials[0]);
                var runtimeMaterial = renderer.sharedMaterials[0];
                var textureProperty = runtimeMaterial.HasProperty(RuntimeFaceMaterialFactory.BaseMapProperty)
                    ? RuntimeFaceMaterialFactory.BaseMapProperty
                    : RuntimeFaceMaterialFactory.MainTexProperty;
                Assert.AreSame(texture, runtimeMaterial.GetTexture(textureProperty));
                Assert.AreEqual(Vector2.one, runtimeMaterial.GetTextureScale(textureProperty));
                Assert.AreEqual(Vector2.zero, runtimeMaterial.GetTextureOffset(textureProperty));
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

                var rendered = sut.TryRender(face, out var warning);

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

                Assert.True(sut.TryRender(firstFace, out var firstWarning), firstWarning);
                Assert.True(sut.TryRender(secondFace, out var secondWarning), secondWarning);
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

                var rendered = sut.TryRender(face, out var warning);

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

        private static GameObject CreateTarget(string name)
        {
            var target = new GameObject(name);
            var renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Standard"));
            target.AddComponent<MeshFilter>();
            return target;
        }

        private static RuntimeFace CreateFace(Transform target, Texture2D texture, Texture2D mask, string id = "front")
        {
            return new RuntimeFace(
                new MachineRuntimeFaceReference { faceId = id, cabinetFaceTargetId = "front" },
                new FaceRuntimeManifest { schemaVersion = 1, faceId = id, width = 1, height = 1, artwork = "artwork.png", mask = "mask.png" },
                target,
                new RuntimeTextureAsset("artwork.png", texture),
                new RuntimeTextureAsset("mask.png", mask));
        }
    }
}
