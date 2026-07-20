using System.IO;
using System.Linq;
using NUnit.Framework;
using OasisPlayer.RuntimeBuild;
using UnityEngine;

namespace OasisPlayer.Tests
{
    public sealed class RuntimeBuildLoaderManifestTests
    {
        [TestCase(RuntimeFaceFrontSideExtensions.NormalValue, RuntimeFaceFrontSide.Normal, false)]
        [TestCase(RuntimeFaceFrontSideExtensions.InvertedValue, RuntimeFaceFrontSide.Inverted, true)]
        public void MachineRuntimeJsonLoadsFrontSide(string frontSide, RuntimeFaceFrontSide expected, bool expectedInverted)
        {
            var root = Path.Combine(Application.temporaryCachePath, "OasisRuntimeBuildLoaderTests", System.Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "cabinet"));
                File.WriteAllBytes(Path.Combine(root, "cabinet", "cabinet.glb"), new byte[] { 1, 2, 3 });
                File.WriteAllText(Path.Combine(root, "cabinet", "cabinet.runtime.json"), "{\"schema\":\"oasis.cabinet.runtime\",\"schemaVersion\":1,\"cabinetId\":\"cabinet\",\"glb\":\"cabinet.glb\",\"scale\":1,\"upAxis\":\"Y\"}");
                File.WriteAllText(Path.Combine(root, "machine.runtime.json"), "{\"schema\":\"oasis.machine.runtime\",\"schemaVersion\":3,\"machineId\":\"machine\",\"displayName\":\"machine\",\"cabinetManifest\":\"cabinet/cabinet.runtime.json\",\"faces\":[{\"faceId\":\"face\",\"assetName\":\"Face\",\"cabinetFaceTargetId\":\"target\",\"frontSide\":\"" + frontSide + "\",\"manifest\":\"faces/Face/face.runtime.json\"}]}");

                Assert.True(RuntimeBuildLoader.TryLoad(root, out var build, out var error), error);
                var reference = build.Faces.Single();

                Assert.AreEqual(frontSide, reference.frontSide);
                Assert.AreEqual(expected, RuntimeFaceFrontSideExtensions.Parse(reference.frontSide));
                Assert.AreEqual(expectedInverted, reference.IsInverted());
                Assert.AreEqual(90, reference.faceRotation);
                Assert.True(reference.faceFlipHorizontal);

                var target = new GameObject("OasisFace_Target");
                var artworkTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                var maskTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                try
                {
                    var runtimeFace = new RuntimeFace(
                        reference,
                        new FaceRuntimeManifest { schemaVersion = 2, faceId = "face", width = 1, height = 1, artwork = "artwork.png", mask = "mask.png" },
                        target.transform,
                        new RuntimeTextureAsset("artwork.png", artworkTexture),
                        new RuntimeTextureAsset("mask.png", maskTexture));

                    Assert.AreSame(reference, runtimeFace.Reference);
                    Assert.AreEqual(expectedInverted, runtimeFace.Reference.IsInverted());
                    Assert.AreEqual(90, runtimeFace.Reference.faceRotation);
                    Assert.True(runtimeFace.Reference.faceFlipHorizontal);
                }
                finally
                {
                    Object.DestroyImmediate(target);
                    Object.DestroyImmediate(artworkTexture);
                    Object.DestroyImmediate(maskTexture);
                }
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
