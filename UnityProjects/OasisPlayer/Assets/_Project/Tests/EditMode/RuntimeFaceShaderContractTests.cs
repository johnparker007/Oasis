using System.IO;
using NUnit.Framework;
using OasisPlayer.RuntimeBuild;
using UnityEngine;

namespace OasisPlayer.Tests
{
    public sealed class RuntimeFaceShaderContractTests
    {
        private const string CommonIncludePath = "Assets/_Project/Shaders/Includes/OasisFaceLampCommon.hlsl";

        [Test]
        public void OasisFaceDeclaresEveryRuntimeMaterialProperty()
        {
            var shader = Shader.Find(RuntimeFaceShaderProperties.ShaderName);
            Assert.NotNull(shader);
            var material = new Material(shader);

            try
            {
                var requiredProperties = new[]
                {
                    RuntimeFaceShaderProperties.ArtworkTextureName,
                    RuntimeFaceShaderProperties.MaskTextureName,
                    RuntimeFaceShaderProperties.TrayIdTextureName,
                    RuntimeFaceShaderProperties.LampIds0TextureName,
                    RuntimeFaceShaderProperties.LampWeights0TextureName,
                    RuntimeFaceShaderProperties.LampStateTextureName,
                    RuntimeFaceShaderProperties.LampExposureStopsName,
                    RuntimeFaceShaderProperties.StaticBrightnessName,
                    RuntimeFaceShaderProperties.BaseAmbientStrengthName,
                    RuntimeFaceShaderProperties.BaseMainLightStrengthName,
                    RuntimeFaceShaderProperties.BaseAdditionalLightStrengthName,
                    RuntimeFaceShaderProperties.MaskStrengthName,
                    RuntimeFaceShaderProperties.NormalSignName,
                    RuntimeFaceShaderProperties.CullModeName,
                    RuntimeFaceShaderProperties.FaceRotationQuarterTurnsName,
                    RuntimeFaceShaderProperties.FaceFlipHorizontalName
                };

                foreach (var property in requiredProperties)
                {
                    Assert.True(material.HasProperty(property), $"Oasis/Face is missing runtime property {property}.");
                }
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void LampLookupUsesZeroAsSentinelAndSupportsBoundaryIds()
        {
            var brightness = new float[256];
            brightness[0] = 1f;
            brightness[1] = 0.25f;
            brightness[255] = 0.75f;

            Assert.AreEqual(0f, RuntimeFaceLampLookupDecoder.Accumulate(brightness, new[] { 0, 0, 0 }, new[] { 255, 255, 255 }));
            Assert.AreEqual(0.25f, RuntimeFaceLampLookupDecoder.Accumulate(brightness, new[] { 1, 0, 0 }, new[] { 255, 255, 255 }));
            Assert.AreEqual(0.75f, RuntimeFaceLampLookupDecoder.Accumulate(brightness, new[] { 255, 0, 0 }, new[] { 255, 255, 255 }));
        }

        [Test]
        public void ThreeChannelWeightedAccumulationUsesByteWeights()
        {
            var brightness = new float[256];
            brightness[1] = 0.2f;
            brightness[127] = 0.4f;
            brightness[255] = 0.6f;

            var actual = RuntimeFaceLampLookupDecoder.Accumulate(brightness, new[] { 1, 127, 255 }, new[] { 255, 128, 64 });
            var expected = 0.2f + 0.4f * (128f / 255f) + 0.6f * (64f / 255f);
            Assert.AreEqual(expected, actual, 0.000001f);
        }

        [Test]
        public void SharedIncludePreservesFaceUvRotationThenHorizontalFlip()
        {
            var source = File.ReadAllText(CommonIncludePath);
            StringAssert.Contains("float2(1.0 - uv.y, uv.x)", source);
            StringAssert.Contains("float2(1.0 - uv.x, 1.0 - uv.y)", source);
            StringAssert.Contains("float2(uv.y, 1.0 - uv.x)", source);
            StringAssert.Contains("if (_OasisFaceFlipHorizontal >= 0.5)", source);
            StringAssert.Contains("transformed.x = 1.0 - transformed.x", source);
            Assert.Less(source.IndexOf("float2(uv.y, 1.0 - uv.x)"), source.IndexOf("if (_OasisFaceFlipHorizontal >= 0.5)"));
        }
    }
}
